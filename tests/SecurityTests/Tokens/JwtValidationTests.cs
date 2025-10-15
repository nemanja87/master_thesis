using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Linq;
using Microsoft.IdentityModel.Tokens;
using OrderService.Security;
using Xunit;

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
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("secretsecretsecretsecret"));
        var token = new JwtSecurityToken(
            issuer: "issuer",
            audience: "aud",
            claims: new List<Claim>(),
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        var unsignedToken = new JwtSecurityToken(token.Issuer, token.Audiences.First(), token.Claims, token.ValidFrom, token.ValidTo);
        var unsignedTokenString = handler.WriteToken(unsignedToken);

        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(unsignedTokenString, new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            RequireSignedTokens = true
        }, out _));
    }
}
