using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OrderService.Security;

namespace SecurityTests.Tokens;

public sealed class JwtValidationTests
{
    [Theory]
    [InlineData("orders.read", true)]
    [InlineData("orders.write", false)]
    public void ScopeHelper_DetectsMissingScopes(string requiredScope, bool expected)
    {
        var claims = new[] { new Claim("scope", "orders.read") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var result = ScopeHelper.HasScope(principal, requiredScope);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void JwtHandler_RejectsAlgNoneTokens()
    {
        var handler = new JwtSecurityTokenHandler { InboundClaimTypeMap = new Dictionary<string, string>() };
        var token = new JwtSecurityToken(headers: new JwtHeader(new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes("secretsecretsecretsecret")), SecurityAlgorithms.HmacSha256)),
            payload: new JwtPayload("issuer", "aud", new List<Claim>(), DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5)));
        var unsignedToken = new JwtSecurityToken(token.Issuer, token.Audiences.First(), token.Claims, token.ValidFrom, token.ValidTo, null);

        Assert.Throws<SecurityTokenException>(() => handler.ValidateToken(unsignedToken.RawData, new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("secretsecretsecretsecret")),
            RequireSignedTokens = true
        }, out _));
    }
}
