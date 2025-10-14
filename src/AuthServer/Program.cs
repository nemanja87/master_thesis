using System;
using System.Collections.Generic;
using AuthServer.Data;
using AuthServer.Infrastructure;
using System.Linq;
using AuthServer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Shared.Security;

var builder = WebApplication.CreateBuilder(args);

var securityProfile = SecurityProfileDefaults.ResolveCurrentProfile();
var requiresHttps = securityProfile.RequiresHttps();
var requiresMtls = securityProfile.RequiresMtls();

var configuration = builder.Configuration;
var openIddictSettings = configuration.GetSection("OpenIddict");
var useDevelopmentCertificates = openIddictSettings.GetValue("UseDevelopmentCertificates", true);
var issuerUri = openIddictSettings.GetValue<string?>("Issuer");

if (requiresHttps)
{
    builder.WebHost.ConfigureKestrel((_, options) =>
    {
        options.ConfigureHttpsDefaults(httpsOptions =>
        {
            httpsOptions.ClientCertificateMode = requiresMtls
                ? Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode.RequireCertificate
                : Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode.AllowCertificate;

            if (requiresMtls)
            {
                httpsOptions.ClientCertificateValidation = (_, _, _) => true;
            }
        });
    });
}

builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseInMemoryDatabase("auth-db");
    options.UseOpenIddict();
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "AuthServer.Identity";
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
});

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<AuthDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetUserinfoEndpointUris("/connect/userinfo")
               .SetIntrospectionEndpointUris("/connect/introspect")
               .SetRevocationEndpointUris("/connect/revocation");

        options.AllowAuthorizationCodeFlow()
               .AllowClientCredentialsFlow()
               .AllowRefreshTokenFlow();

        options.RegisterScopes(OpenIddictConstants.Scopes.OpenId,
                               OpenIddictConstants.Scopes.Profile,
                               "orders.read",
                               "orders.write",
                               "inventory.write");

        if (!string.IsNullOrWhiteSpace(issuerUri) && Uri.TryCreate(issuerUri, UriKind.Absolute, out var issuer))
        {
            options.SetIssuer(issuer);
        }

        if (useDevelopmentCertificates)
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }

        var aspNetCoreServer = options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableUserinfoEndpointPassthrough()
            .EnableLogoutEndpointPassthrough()
            .EnableStatusCodePagesIntegration();

        if (!requiresHttps || !builder.Environment.IsProduction())
        {
            aspNetCoreServer.DisableTransportSecurityRequirement();
        }
    });

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
});

builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews()
    .AddApplicationPart(typeof(OpenIddictServerAspNetCoreBuilder).Assembly);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

await app.SeedAsync();

if (requiresHttps)
{
    app.UseHttpsRedirection();
}

if (requiresMtls)
{
    var mtlsLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AuthServer.mTLS");
    app.Use(async (context, next) =>
    {
        var certificate = context.Connection.ClientCertificate ?? await context.Connection.GetClientCertificateAsync();
        if (certificate is null)
        {
            mtlsLogger.LogWarning("Client certificate required but missing for {Remote}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Client certificate required.");
            return;
        }

        await next();
    });
}

app.UseRouting();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/account/login", async (LoginRequest request,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) =>
{
    if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Username and password are required." });
    }

    var user = await userManager.FindByNameAsync(request.UserName)
               ?? await userManager.FindByEmailAsync(request.UserName);

    if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
    {
        return Results.BadRequest(new { error = "Invalid credentials." });
    }

    await signInManager.SignInAsync(user, isPersistent: false);
    return Results.Ok(new { user = user.UserName });
});

app.MapPost("/account/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.NoContent();
});

app.MapGet("/", () => Results.Ok("AuthServer running."));

app.MapControllers();
app.MapDefaultControllerRoute();

app.Run();

internal sealed record LoginRequest(string UserName, string Password);
