using System.ComponentModel.DataAnnotations;

namespace ShareSync.Application.DTOs.Dividends;

public class CreateDividendRequestDto
{
    [Required(ErrorMessage = "Company ID is required.")]
    public int CompanyId { get; set; }

    [Required(ErrorMessage = "Dividend per share is required.")]
    [Range(0.01, 999999999999.99, ErrorMessage = "Dividend per share must be greater than zero.")]
    public decimal DividendPerShare { get; set; }

    [Required(ErrorMessage = "Declaration date is required.")]
    public DateTime DeclarationDate { get; set; }

    [Required(ErrorMessage = "Payment date is required.")]
    public DateTime PaymentDate { get; set; }
}
