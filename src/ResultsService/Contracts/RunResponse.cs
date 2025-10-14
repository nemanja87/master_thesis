using System.Collections.Generic;
using ResultsService.Models;

namespace ResultsService.Contracts;

public sealed class RunResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string Configuration { get; init; } = string.Empty;
    public IReadOnlyList<MetricResponse> Metrics { get; init; } = Array.Empty<MetricResponse>();

    public static RunResponse FromEntity(Run run)
    {
        return new RunResponse
        {
            Id = run.Id,
            Name = run.Name,
            Environment = run.Environment,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            Configuration = run.ConfigurationJson,
            Metrics = run.Metrics.Select(metric => new MetricResponse
            {
                Id = metric.Id,
                Name = metric.Name,
                Unit = metric.Unit,
                Value = metric.Value
            }).ToList()
        };
    }
}

public sealed class MetricResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public double Value { get; init; }
}
