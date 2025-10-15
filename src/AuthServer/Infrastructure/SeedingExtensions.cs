using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AuthServer.Data;
using AuthServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Infrastructure;

internal static class SeedingExtensions
{
    public static async Task SeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await EnsureRoleAsync(roleManager, "User");
        await EnsureRoleAsync(roleManager, "Admin");

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(userManager, "user@example.com", "User123!", new[] { "User" });
        await EnsureUserAsync(userManager, "admin@example.com", "Admin123!", new[] { "Admin" });

        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        await EnsureScopeAsync(scopeManager, "orders.read", "Read access to orders", "orders-api");
        await EnsureScopeAsync(scopeManager, "orders.write", "Write access to orders", "orders-api");
        await EnsureScopeAsync(scopeManager, "inventory.write", "Write access to inventory", "inventory-api");

        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var clientsSection = app.Configuration.GetSection("OpenIddict:Clients");

        await EnsureBenchUiClientAsync(applicationManager, clientsSection);
        await EnsureBenchRunnerClientAsync(applicationManager, clientsSection);
        await EnsureServiceOrderClientAsync(applicationManager, clientsSection);
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string password, IEnumerable<string> roles)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create seed user '{email}': {errors}");
            }
        }

        foreach (var role in roles)
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }

    private static async Task EnsureScopeAsync(IOpenIddictScopeManager scopeManager, string name, string displayName, string resource)
    {
        var existing = await scopeManager.FindByNameAsync(name, CancellationToken.None);
        if (existing is not null)
        {
            return;
        }

        var descriptor = new OpenIddictScopeDescriptor
        {
            Name = name,
            DisplayName = displayName
        };

        descriptor.Resources.Add(resource);

        await scopeManager.CreateAsync(descriptor, CancellationToken.None);
    }

    private static async Task EnsureBenchUiClientAsync(IOpenIddictApplicationManager manager, IConfiguration clientsSection)
    {
        const string clientId = "bench-ui";
        if (await manager.FindByClientIdAsync(clientId, CancellationToken.None) is not null)
        {
            return;
        }

        var clientSection = clientsSection.GetSection(clientId);
        var redirectUri = clientSection.GetValue<string?>("RedirectUri") ?? "https://127.0.0.1:5173/auth/callback";
        var postLogoutRedirectUri = clientSection.GetValue<string?>("PostLogoutRedirectUri") ?? "https://127.0.0.1:5173/";

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = "Bench UI",
            ConsentType = ConsentTypes.Explicit,
            Type = ClientTypes.Public
        };

        descriptor.RedirectUris.Add(new Uri(redirectUri));
        descriptor.PostLogoutRedirectUris.Add(new Uri(postLogoutRedirectUri));

        descriptor.Permissions.UnionWith(new[]
        {
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Scope + Scopes.OpenId,
            Permissions.Prefixes.Scope + Scopes.Profile,
            Permissions.Prefixes.Scope + "orders.read",
            Permissions.Prefixes.Scope + "orders.write"
        });

        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);

        await manager.CreateAsync(descriptor, CancellationToken.None);
    }

    private static async Task EnsureBenchRunnerClientAsync(IOpenIddictApplicationManager manager, IConfiguration clientsSection)
    {
        const string clientId = "bench-runner";
        var existing = await manager.FindByClientIdAsync(clientId, CancellationToken.None);
        if (existing is not null)
        {
            var existingDescriptor = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(existing, existingDescriptor, CancellationToken.None);

            existingDescriptor.Permissions.Add(Permissions.Prefixes.Scope + "orders.read");
            existingDescriptor.Permissions.Add(Permissions.Prefixes.Scope + "orders.write");
            existingDescriptor.Permissions.Add(Permissions.Prefixes.Scope + "inventory.write");

            await manager.UpdateAsync(existing, existingDescriptor, CancellationToken.None);
            return;
        }

        var clientSection = clientsSection.GetSection(clientId);
        var clientSecret = clientSection.GetValue<string?>("ClientSecret") ?? "bench-runner-secret";

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = "Bench Runner",
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.Prefixes.Scope + "orders.read",
                Permissions.Prefixes.Scope + "orders.write",
                Permissions.Prefixes.Scope + "inventory.write"
            },
            Type = ClientTypes.Confidential
        };

        await manager.CreateAsync(descriptor, CancellationToken.None);
    }

    private static async Task EnsureServiceOrderClientAsync(IOpenIddictApplicationManager manager, IConfiguration clientsSection)
    {
        const string clientId = "service-order";
        if (await manager.FindByClientIdAsync(clientId, CancellationToken.None) is not null)
        {
            return;
        }

        var clientSection = clientsSection.GetSection(clientId);
        var clientSecret = clientSection.GetValue<string?>("ClientSecret") ?? "service-order-secret";

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = "Service Order Client",
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.Prefixes.Scope + "inventory.write"
            },
            Type = ClientTypes.Confidential
        };

        await manager.CreateAsync(descriptor, CancellationToken.None);
    }
}
