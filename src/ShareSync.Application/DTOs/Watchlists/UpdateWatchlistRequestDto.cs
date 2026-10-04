using System.ComponentModel.DataAnnotations;

namespace ShareSync.Application.DTOs.Watchlists;

public class UpdateWatchlistRequestDto
{
    [Required(ErrorMessage = "Watchlist name is required.")]
    [StringLength(100, ErrorMessage = "Watchlist name cannot exceed 100 characters.")]
    public string WatchlistName { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Description cannot exceed 255 characters.")]
    public string? Description { get; set; }
}
