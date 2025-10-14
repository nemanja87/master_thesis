using System.Net.Http.Headers;
using IntegrationTests.Utilities;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests;

public sealed class TlsIntegrationTests : IClassFixture<ResultsServiceFixture>
{
    private readonly ResultsServiceFixture _fixture;

    public TlsIntegrationTests(ResultsServiceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Skip = "Requires running services and valid TLS certificates")]
    public async Task RestGateway_AllowsAuthorizedRequests()
    {
        await using var factory = new WebApplicationFactory<Gateway.Program>();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "REPLACE_WITH_REAL_TOKEN");

        var response = await client.GetAsync("/api/orders");

        Assert.NotEqual(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }
}
