using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResultsService.Models;

public sealed class Run
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Environment { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    [Column(TypeName = "jsonb")]
    public string ConfigurationJson { get; set; } = string.Empty;

    public ICollection<Metric> Metrics { get; set; } = new List<Metric>();
}
