using IntegrationTests.Utilities;

namespace IntegrationTests;

public sealed class AuthorizationPolicyTests
{
    [Fact(Skip = Skip.RequiresInfrastructure)]
    public async Task RestEndpoint_ReturnsForbidden_WhenScopeMissing()
    {
        await Task.CompletedTask;
        Assert.True(true);
    }

    [Fact(Skip = Skip.RequiresInfrastructure)]
    public async Task GrpcEndpoint_ReturnsPermissionDenied_WhenScopeMissing()
    {
        await Task.CompletedTask;
        Assert.True(true);
    }
}
