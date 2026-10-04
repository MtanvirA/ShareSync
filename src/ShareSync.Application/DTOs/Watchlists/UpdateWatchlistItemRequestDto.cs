using System.ComponentModel.DataAnnotations;

namespace ShareSync.Application.DTOs.Watchlists;

public class UpdateWatchlistItemRequestDto
{
    [Range(0.01, 999999999999.99, ErrorMessage = "Target price must be greater than zero.")]
    public decimal? TargetPrice { get; set; }
}
