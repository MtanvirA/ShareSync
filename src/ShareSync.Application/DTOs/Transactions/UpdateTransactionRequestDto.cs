using System.ComponentModel.DataAnnotations;

namespace ShareSync.Application.DTOs.Transactions;

public class UpdateTransactionRequestDto : IValidatableObject
{
    [Required(ErrorMessage = "Quantity is required.")]
    [Range(1, 100_000_000, ErrorMessage = "Quantity must be at least 1 whole share.")]
    public decimal Quantity { get; set; }

    [Required(ErrorMessage = "Price per share is required.")]
    [Range(0.01, 100_000_000, ErrorMessage = "Price per share must be greater than zero.")]
    public decimal PricePerShare { get; set; }

    public DateTime? TransactionDate { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Quantity != Math.Floor(Quantity))
        {
            yield return new ValidationResult(
                "Fractional shares are not supported. Quantity must be a whole integer.",
                new[] { nameof(Quantity) });
        }
    }
}
