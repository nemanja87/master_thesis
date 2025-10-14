using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ResultsService.Contracts;

public sealed class RunRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Environment { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string Configuration { get; set; } = "{}";

    public List<MetricRequest> Metrics { get; set; } = new();
}

public sealed class MetricRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Unit { get; set; } = string.Empty;

    public double Value { get; set; }
}
