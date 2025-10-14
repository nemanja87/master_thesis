using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServer.Controllers;

[ApiController]
public sealed class AuthorizationController : Controller
{
    private readonly IOpenIddictApplicationManager _applications;

    public AuthorizationController(IOpenIddictApplicationManager applications)
    {
        _applications = applications;
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange()
    {
        if (!HttpContext.Request.HasFormContentType)
        {
            return BadRequest(new { error = Errors.InvalidRequest });
        }

        var form = await HttpContext.Request.ReadFormAsync(HttpContext.RequestAborted);
        var grantType = form[Parameters.GrantType].ToString();
        if (!string.Equals(grantType, GrantTypes.ClientCredentials, StringComparison.Ordinal))
        {
            return BadRequest(new { error = Errors.UnsupportedGrantType });
        }

        var clientId = form[Parameters.ClientId].ToString();
        var clientSecret = form[Parameters.ClientSecret].ToString();

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return BadRequest(new { error = Errors.InvalidClient });
        }

        var application = await _applications.FindByClientIdAsync(clientId, HttpContext.RequestAborted);
        if (application is null)
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (!string.IsNullOrEmpty(clientSecret) &&
            !await _applications.ValidateClientSecretAsync(application, clientSecret, HttpContext.RequestAborted))
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.SetClaim(Claims.Subject, clientId);
        identity.SetClaim(Claims.ClientId, clientId);

        var principal = new ClaimsPrincipal(identity);
        var requestedScopes = form[Parameters.Scope].ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (requestedScopes.Length > 0)
        {
            principal.SetScopes(requestedScopes);
        }

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
