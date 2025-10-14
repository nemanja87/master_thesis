using System.Collections.Generic;

namespace BenchRunner.Models;

internal sealed class RunSubmission
{
    public string Name { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public string Configuration { get; init; } = string.Empty;
    public List<MetricSubmission> Metrics { get; init; } = new();
}

internal sealed class MetricSubmission
{
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public double Value { get; init; }
}
