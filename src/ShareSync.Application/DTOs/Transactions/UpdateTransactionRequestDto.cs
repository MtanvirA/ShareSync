using System.ComponentModel.DataAnnotations;

namespace ShareSync.Application.DTOs.Transactions;

public class UpdateTransactionRequestDto
{
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
