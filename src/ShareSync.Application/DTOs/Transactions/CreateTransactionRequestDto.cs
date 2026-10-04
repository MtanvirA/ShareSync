using System.ComponentModel.DataAnnotations;

namespace ShareSync.Application.DTOs.Transactions;

public class CreateTransactionRequestDto
{
    [Required(ErrorMessage = "Portfolio ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "A valid portfolio must be selected.")]
    public int PortfolioId { get; set; }

    [Required(ErrorMessage = "Company ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "A valid company must be selected.")]
    public int CompanyId { get; set; }

    [Required(ErrorMessage = "Transaction type is required.")]
    [RegularExpression("^(?i)(BUY|SELL)$", ErrorMessage = "Transaction type must be either 'BUY' or 'SELL'.")]
    public string TransactionType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Quantity is required.")]
    [Range(0.0001, 100_000_000, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    [Required(ErrorMessage = "Price per share is required.")]
    [Range(0.01, 100_000_000, ErrorMessage = "Price per share must be greater than zero.")]
    public decimal PricePerShare { get; set; }

    public DateTime? TransactionDate { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }
}
