using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Gateway.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.IdentityModel.Tokens;
using Shared.Security;
using Yarp.ReverseProxy.Transforms;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

var securityProfile = SecurityProfileDefaults.ResolveCurrentProfile();
var requiresHttps = securityProfile.RequiresHttps();
var requiresMtls = securityProfile.RequiresMtls();
var requiresJwt = securityProfile.RequiresJwt();

var handshakeTracker = new HandshakeTracker();
builder.Services.AddSingleton(handshakeTracker);

builder.WebHost.ConfigureKestrel((context, options) =>
{
    var configuration = context.Configuration;
    var certificatePath = configuration.GetValue<string?>("Security:CertificatePath");
    var certificatePassword = configuration.GetValue<string?>("Security:CertificatePassword");

    ConfigureListener(options, 8080, HttpProtocols.Http1, certificatePath, certificatePassword, requiresHttps, requiresMtls, handshakeTracker);
    ConfigureListener(options, 9090, HttpProtocols.Http2, certificatePath, certificatePassword, requiresHttps, requiresMtls, handshakeTracker);
});

var authority = builder.Configuration["AuthServer:Authority"];

if (requiresJwt)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = authority;
            options.RequireHttpsMetadata = requiresHttps;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = false
            };
        });

    builder.Services.AddAuthorization();
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformBuilderContext =>
    {
        transformBuilderContext.AddRequestTransform(transformContext =>
        {
            if (transformContext.HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                transformContext.ProxyRequest.Headers.Remove("Authorization");
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("Authorization", authHeader.ToArray());
            }

            foreach (var header in transformContext.HttpContext.Request.Headers)
            {
                if (header.Key.StartsWith("grpc-", StringComparison.OrdinalIgnoreCase))
                {
                    transformContext.ProxyRequest.Headers.Remove(header.Key);
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();

handshakeTracker.Initialize(app.Services.GetRequiredService<ILoggerFactory>());
app.Lifetime.ApplicationStopping.Register(handshakeTracker.LogSummary);

// Instrument HTTP request metrics and expose /metrics for Prometheus
app.UseHttpMetrics();

app.UseCors();

if (requiresHttps)
{
    app.UseHttpsRedirection();
}

if (requiresJwt)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

var payloadLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Gateway.Payloads");

app.Use(async (context, next) =>
{
    var requestSize = GetRequestSize(context);
    if (requestSize > 0)
    {
        payloadLogger.LogInformation(
            "Incoming {Protocol} {Method} {Path} payload size {Bytes} bytes",
            context.Request.Protocol,
            context.Request.Method,
            context.Request.Path,
            requestSize);
    }

    context.Response.OnStarting(() =>
    {
        if (context.Response.ContentLength.HasValue)
        {
            payloadLogger.LogInformation(
                "Outgoing {StatusCode} {Path} payload size {Bytes} bytes",
                context.Response.StatusCode,
                context.Request.Path,
                context.Response.ContentLength.Value);
        }

        return Task.CompletedTask;
    });

    await next();
});

app.MapGet("/", () => Results.Ok("Gateway ready."));
app.MapGet("/healthz", () => Results.Ok(new
{
    status = "healthy",
    handshakes = handshakeTracker.HandshakeCount,
    clientCertificates = handshakeTracker.ClientCertificateCount
}));

app.MapMetrics();

app.MapReverseProxy();

app.Run();

static void ConfigureListener(KestrelServerOptions options,
    int port,
    HttpProtocols protocols,
    string? certificatePath,
    string? certificatePassword,
    bool useHttps,
    bool enableMtls,
    HandshakeTracker handshakeTracker)
{
    options.ListenAnyIP(port, listenOptions =>
    {
        listenOptions.Protocols = protocols;
        if (useHttps)
        {
            ConfigureHttps(listenOptions, certificatePath, certificatePassword, enableMtls, handshakeTracker);
        }
    });
}

static void ConfigureHttps(ListenOptions listenOptions,
    string? certificatePath,
    string? certificatePassword,
    bool enableMtls,
    HandshakeTracker handshakeTracker)
{
    void Configure(HttpsConnectionAdapterOptions httpsOptions) =>
        ConfigureHttpsOptions(httpsOptions, enableMtls, handshakeTracker);

    if (!string.IsNullOrWhiteSpace(certificatePath))
    {
        var certificate = string.IsNullOrEmpty(certificatePassword)
            ? new X509Certificate2(certificatePath)
            : new X509Certificate2(certificatePath, certificatePassword);

        listenOptions.UseHttps(certificate, Configure);
    }
    else
    {
        listenOptions.UseHttps(Configure);
    }
}

static void ConfigureHttpsOptions(HttpsConnectionAdapterOptions httpsOptions,
    bool enableMtls,
    HandshakeTracker handshakeTracker)
{
    httpsOptions.ClientCertificateMode = enableMtls
        ? ClientCertificateMode.RequireCertificate
        : ClientCertificateMode.AllowCertificate;

    httpsOptions.ClientCertificateValidation = (certificate, chain, errors) =>
    {
        handshakeTracker.RecordClientCertificate(certificate, errors);
        return true;
    };

    httpsOptions.OnAuthenticate = (connectionContext, sslOptions) =>
    {
        handshakeTracker.RecordHandshake(connectionContext, sslOptions);
    };
}

static long GetRequestSize(HttpContext context)
{
    if (context.Request.ContentLength.HasValue)
    {
        return context.Request.ContentLength.Value;
    }

    if (context.Request.Headers.TryGetValue("Content-Length", out var headerValue) &&
        long.TryParse(headerValue, out var parsed))
    {
        return parsed;
    }

    return 0;
}
