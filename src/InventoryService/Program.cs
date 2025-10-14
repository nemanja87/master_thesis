using Microsoft.AspNetCore.Http;
using InventoryService.Grpc;
using InventoryService.Models;
using InventoryService.Security;
using InventoryService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Shared.Security;

// Enable HTTP/2 without TLS (H2C) for S0 profile
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

var securityProfile = SecurityProfileDefaults.ResolveCurrentProfile();
var requiresHttps = securityProfile.RequiresHttps();
var requiresMtls = securityProfile.RequiresMtls();
var requiresJwt = securityProfile.RequiresJwt();
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
        // Port 8082: REST endpoint (HTTP/1.1)
        options.ListenAnyIP(8082, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
        });

        // Port 9092: gRPC endpoint (HTTP/2 cleartext)
        options.ListenAnyIP(9092, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        });
    }
});

builder.Services.AddSingleton<IInventoryStore, InMemoryInventoryStore>();

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
        options.AddPolicy("InventoryWrite", policy =>
        {
            policy.RequireAuthenticatedUser();
            if (requiresPolicies)
            {
                policy.RequireAssertion(ctx => ctx.User is not null && ScopeHelper.HasScope(ctx.User, "inventory.write"));
            }
        });
    });
}

var app = builder.Build();

if (requiresHttps)
{
    app.UseHttpsRedirection();
}

if (requiresMtls)
{
    var mtlsLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("InventoryService.mTLS");
    app.Use(async (context, next) =>
    {
        var certificate = context.Connection.ClientCertificate ?? await context.Connection.GetClientCertificateAsync();
        if (certificate is null)
        {
            mtlsLogger.LogWarning("mTLS enabled but request from {Remote} missing client certificate.", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Client certificate required.");
            return;
        }

        mtlsLogger.LogInformation("Accepted client certificate subject {Subject}", certificate.Subject);
        await next();
    });
}

if (requiresJwt || requiresPolicies)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

var reservationEndpoint = app.MapPost("/api/inventory/reservations", async (InventoryReservationRequest request, IInventoryStore store, CancellationToken cancellationToken) =>
    {
        if (request is null)
        {
            return Results.BadRequest(new { error = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            return Results.BadRequest(new { error = "OrderId is required." });
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return Results.BadRequest(new { error = "At least one item is required." });
        }

        var normalized = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Sku))
            {
                return Results.BadRequest(new { error = "Item SKU is required." });
            }

            if (item.Quantity <= 0)
            {
                return Results.BadRequest(new { error = "Item quantity must be positive." });
            }

            normalized[item.Sku] = normalized.TryGetValue(item.Sku, out var existing)
                ? existing + item.Quantity
                : item.Quantity;
        }

        var result = await store.ReserveAsync(request.OrderId, normalized, cancellationToken);
        if (!result.Success)
        {
            return Results.Conflict(new InventoryReservationResponse(false, result.RemainingQuantities));
        }

        return Results.Ok(new InventoryReservationResponse(true, result.RemainingQuantities));
    })
    ;
if (requiresPolicies)
{
    reservationEndpoint.RequireAuthorization("InventoryWrite");
}
else if (requiresJwt)
{
    reservationEndpoint.RequireAuthorization();
}

app.MapGrpcService<InventoryGrpcService>();

app.MapGet("/", () => "InventoryService ready.");

app.Run();
