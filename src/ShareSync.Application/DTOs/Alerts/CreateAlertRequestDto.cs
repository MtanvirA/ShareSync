using System.ComponentModel.DataAnnotations;

namespace ShareSync.Application.DTOs.Alerts;

public class CreateAlertRequestDto
{
    [Required(ErrorMessage = "Alert type is required.")]
    [RegularExpression("^(PRICE_ABOVE|PRICE_BELOW|PORTFOLIO_VALUE_ABOVE|PORTFOLIO_VALUE_BELOW)$",
        ErrorMessage = "Invalid alert type. Allowed values: PRICE_ABOVE, PRICE_BELOW, PORTFOLIO_VALUE_ABOVE, PORTFOLIO_VALUE_BELOW.")]
    public string AlertType { get; set; } = string.Empty;

    public int? CompanyId { get; set; }

    public int? PortfolioId { get; set; }

    [Required(ErrorMessage = "Threshold value is required.")]
    [Range(0.01, 10000000000.0, ErrorMessage = "Threshold value must be greater than zero.")]
    public decimal ThresholdValue { get; set; }
}

public class UpdateAlertRequestDto
{
    [Required(ErrorMessage = "Threshold value is required.")]
    [Range(0.01, 10000000000.0, ErrorMessage = "Threshold value must be greater than zero.")]
    public decimal ThresholdValue { get; set; }

    public bool? IsActive { get; set; }
}
