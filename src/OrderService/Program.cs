using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OrderService.Clients;
using OrderService.Configuration;
using OrderService.Grpc;
using OrderService.Security;
using OrderService.Services;
using Shared.Contracts.Orders;
using Shared.Contracts.Protos;
using Shared.Security;
using Prometheus;

// Enable HTTP/2 without TLS (H2C) for S0 profile - both server and client
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

var securityProfile = SecurityProfileDefaults.ResolveCurrentProfile();
var requiresHttps = securityProfile.RequiresHttps();
var requiresJwt = securityProfile.RequiresJwt();
var requiresMtls = securityProfile.RequiresMtls();
var requiresPolicies = securityProfile.RequiresPerMethodPolicies();

builder.WebHost.ConfigureKestrel((_, options) =>
{
    if (requiresHttps)
    {
        options.ConfigureHttpsDefaults(httpsOptions =>
        {
            httpsOptions.ClientCertificateMode = requiresMtls
                ? ClientCertificateMode.RequireCertificate
                : ClientCertificateMode.AllowCertificate;

            if (requiresMtls)
            {
                httpsOptions.ClientCertificateValidation = (_, _, _) => true;
            }
        });
    }
    else
    {
        // S0 profile: Configure specific ports with appropriate protocols
        // Port 8081: REST endpoint (HTTP/1.1)
        options.ListenAnyIP(8081, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
        });

        // Port 9091: gRPC endpoint (HTTP/2 cleartext)
        options.ListenAnyIP(9091, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        });
    }
});

builder.Services.Configure<InventoryOptions>(builder.Configuration.GetSection("Inventory"));

builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddScoped<OrderProcessor>();

if (requiresJwt || requiresPolicies)
{
    builder.Services.AddSingleton(new ScopeAuthorizationInterceptor(securityProfile));
    builder.Services.AddGrpc(options =>
    {
        options.Interceptors.Add<ScopeAuthorizationInterceptor>();
    });
}
else
{
    builder.Services.AddGrpc();
}

builder.Services.AddHttpClient<RestInventoryClient>();

builder.Services.AddGrpcClient<InventoryService.InventoryServiceClient>((services, options) =>
{
    var inventoryOptions = services.GetRequiredService<IOptions<InventoryOptions>>().Value;
    options.Address = new Uri(inventoryOptions.GrpcAddress);
});

builder.Services.AddScoped<IInventoryClient>(sp =>
{
    var inventoryOptions = sp.GetRequiredService<IOptions<InventoryOptions>>().Value;
    if (inventoryOptions.UseGrpc)
    {
        return ActivatorUtilities.CreateInstance<GrpcInventoryClient>(sp);
    }

    return sp.GetRequiredService<RestInventoryClient>();
});

var authority = builder.Configuration["AuthServer:Authority"];

if (requiresJwt)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = authority;
            options.RequireHttpsMetadata = requiresHttps;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = false
            };
        });
}

if (requiresJwt || requiresPolicies)
{
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("OrdersRead", policy =>
        {
            policy.RequireAuthenticatedUser();
            if (requiresPolicies)
            {
                policy.RequireAssertion(ctx => ctx.User is not null && ScopeHelper.HasScope(ctx.User, "orders.read"));
            }
        });

        options.AddPolicy("OrdersWrite", policy =>
        {
            policy.RequireAuthenticatedUser();
            if (requiresPolicies)
            {
                policy.RequireAssertion(ctx => ctx.User is not null && ScopeHelper.HasScope(ctx.User, "orders.write"));
            }
        });
    });
}

var app = builder.Build();

// Instrument HTTP request metrics and expose /metrics for Prometheus
app.UseHttpMetrics();

if (requiresHttps)
{
    app.UseHttpsRedirection();
}

if (requiresMtls)
{
    var mtlsLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("OrderService.mTLS");
    app.Use(async (context, next) =>
    {
        var certificate = context.Connection.ClientCertificate ?? await context.Connection.GetClientCertificateAsync();
        if (certificate is null)
        {
            mtlsLogger.LogWarning("mTLS profile requires client certificate, but none provided by {Remote}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Client certificate required.");
            return;
        }

        mtlsLogger.LogDebug("Accepted client certificate subject {Subject}", certificate.Subject);
        await next();
    });
}

if (requiresJwt || requiresPolicies)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

var createOrderEndpoint = app.MapPost("/api/orders", async (CreateOrderRequest request, OrderProcessor processor, CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            return Results.BadRequest(new { error = "CustomerId is required." });
        }

        if (request.ItemSkus is null || request.ItemSkus.Count == 0)
        {
            return Results.BadRequest(new { error = "At least one item SKU is required." });
        }

        var response = await processor.CreateAsync(request, cancellationToken);
        if (!response.Accepted)
        {
            return Results.StatusCode(StatusCodes.Status409Conflict);
        }

        return Results.Created($"/api/orders/{response.OrderId}", response);
    })
    ;
if (requiresPolicies)
{
    createOrderEndpoint.RequireAuthorization("OrdersWrite");
}
else if (requiresJwt)
{
    createOrderEndpoint.RequireAuthorization();
}

var getOrderEndpoint = app.MapGet("/api/orders/{id}", async (string id, OrderProcessor processor, CancellationToken cancellationToken) =>
    {
        var order = await processor.GetAsync(id, cancellationToken);
        return order is null ? Results.NotFound() : Results.Ok(order);
    })
    ;
if (requiresPolicies)
{
    getOrderEndpoint.RequireAuthorization("OrdersRead");
}
else if (requiresJwt)
{
    getOrderEndpoint.RequireAuthorization();
}

app.MapGrpcService<OrderGrpcService>();

app.MapGet("/", () => "OrderService ready.");

app.MapMetrics();

app.Run();
