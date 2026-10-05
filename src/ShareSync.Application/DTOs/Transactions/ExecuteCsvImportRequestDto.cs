using System.ComponentModel.DataAnnotations;

namespace ShareSync.Application.DTOs.Transactions;

public class ExecuteCsvImportRequestDto
{
    [Required(ErrorMessage = "Portfolio ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "A valid portfolio must be selected.")]
    public int PortfolioId { get; set; }

    [Required(ErrorMessage = "CSV content is required for execution.")]
    public string CsvContent { get; set; } = string.Empty;

    /// <summary>
    /// When false (default), ALL rows must be valid, otherwise NO rows are inserted (Atomic mode).
    /// When true, valid rows are inserted and invalid rows are skipped (Partial mode).
    /// </summary>
    public bool AllowPartialImport { get; set; } = false;
}
