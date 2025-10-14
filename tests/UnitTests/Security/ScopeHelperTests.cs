using System.Security.Claims;
using OrderService.Security;

namespace UnitTests.Security;

public sealed class ScopeHelperTests
{
    [Theory]
    [InlineData("orders.read inventory.write", "orders.read", true)]
    [InlineData("orders.read inventory.write", "orders.write", false)]
    [InlineData("orders.read", "inventory.write", false)]
    [InlineData("", "orders.read", false)]
    public void HasScope_EvaluatesClaimsCorrectly(string scopeClaim, string requestedScope, bool expected)
    {
        var identity = new ClaimsIdentity();
        if (!string.IsNullOrWhiteSpace(scopeClaim))
        {
            identity.AddClaim(new Claim("scope", scopeClaim));
        }

        var principal = new ClaimsPrincipal(identity);

        var actual = ScopeHelper.HasScope(principal, requestedScope);

        Assert.Equal(expected, actual);
    }
}
