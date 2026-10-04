using System.ComponentModel.DataAnnotations;

namespace ShareSync.Application.DTOs.Portfolios;

public class UpdatePortfolioRequestDto
{
    [Required(ErrorMessage = "Portfolio name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Portfolio name must be between 1 and 100 characters.")]
    public string PortfolioName { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Description cannot exceed 255 characters.")]
    public string? Description { get; set; }
}
