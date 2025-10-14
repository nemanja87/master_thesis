using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace IntegrationTests;

public sealed class ResultsServiceFixture : IAsyncLifetime
{
    private IContainer? _postgres;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        const string postgresPassword = "user";

        _postgres = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_PASSWORD", postgresPassword)
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_DB", "results-db")
            .WithPortBinding(5433, 5432)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await _postgres.StartAsync();
        ConnectionString = $"Host=localhost;Port=5433;Database=results-db;Username=postgres;Password={postgresPassword}";
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.StopAsync();
        }
    }
}
