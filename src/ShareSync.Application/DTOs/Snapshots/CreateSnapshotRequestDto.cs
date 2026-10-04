using System.ComponentModel.DataAnnotations;

namespace ShareSync.Application.DTOs.Snapshots;

public class CreateSnapshotRequestDto
{
    [Required(ErrorMessage = "Snapshot date is required.")]
    public DateTime? SnapshotDate { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Total value cannot be negative.")]
    public decimal? TotalValue { get; set; }
}
