using Microsoft.Extensions.Diagnostics.HealthChecks;
using ResultsService.Data;

namespace ResultsService.HealthChecks;

internal sealed class ResultsDatabaseHealthCheck : IHealthCheck
{
    private readonly ResultsDbContext _dbContext;

    public ResultsDatabaseHealthCheck(ResultsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Unable to connect to the results database.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database connectivity check failed.", exception);
        }
    }
}
