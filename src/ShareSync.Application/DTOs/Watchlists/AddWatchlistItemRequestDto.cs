using System.ComponentModel.DataAnnotations;

namespace ShareSync.Application.DTOs.Watchlists;

public class AddWatchlistItemRequestDto
{
    [Required(ErrorMessage = "Company ID is required.")]
    public int CompanyId { get; set; }

    [Range(0.01, 999999999999.99, ErrorMessage = "Target price must be greater than zero.")]
    public decimal? TargetPrice { get; set; }
}
