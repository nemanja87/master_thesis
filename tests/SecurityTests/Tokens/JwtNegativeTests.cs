using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace SecurityTests.Tokens;

public sealed class JwtNegativeTests
{
    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("01234567890123456789012345678901"));

    [Fact]
    public void ValidateToken_ThrowsForExpiredToken()
    {
        var token = CreateToken(DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5));
        Assert.Throws<SecurityTokenExpiredException>(() => Validate(token));
    }

    [Fact]
    public void ValidateToken_ThrowsForWrongAudience()
    {
        var token = CreateToken(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5), audience: "wrong");
        Assert.Throws<SecurityTokenInvalidAudienceException>(() => Validate(token));
    }

    [Fact]
    public void ValidateToken_ThrowsForMissingScopeClaim()
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Audience = "api",
            Issuer = "issuer",
            Subject = new ClaimsIdentity(new[] { new Claim("sub", "user") }),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        });

        var principal = Validate(token);
        Assert.DoesNotContain(principal.Claims, c => c.Type == "scope");
    }

    private static string CreateToken(DateTime notBefore, DateTime expires, string audience = "api")
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Audience = audience,
            Issuer = "issuer",
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", "user"),
                new Claim("scope", "orders.read")
            }),
            NotBefore = notBefore,
            Expires = expires,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        });
    }

    private static ClaimsPrincipal Validate(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "issuer",
            ValidateAudience = true,
            ValidAudience = "api",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        }, out _);
    }
}
