using System.ComponentModel.DataAnnotations;

namespace ShareSync.Application.DTOs.Transactions;

public class ValidateCsvImportRequestDto
{
    [Required(ErrorMessage = "Portfolio ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "A valid portfolio must be selected.")]
    public int PortfolioId { get; set; }

    public string? CsvContent { get; set; }

    public string? FileName { get; set; }
}
