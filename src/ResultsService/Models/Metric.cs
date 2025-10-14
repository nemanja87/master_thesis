using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResultsService.Models;

public sealed class Metric
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid RunId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Unit { get; set; } = string.Empty;

    public double Value { get; set; }

    [ForeignKey(nameof(RunId))]
    public Run? Run { get; set; }
}
