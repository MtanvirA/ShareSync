namespace ShareSync.Application.DTOs.Alerts;

public class AlertDto
{
    public int AlertId { get; set; }
    public int UserId { get; set; }
    public int? CompanyId { get; set; }
    public string? TickerSymbol { get; set; }
    public string? CompanyName { get; set; }
    public decimal? CurrentPrice { get; set; }

    public int? PortfolioId { get; set; }
    public string? PortfolioName { get; set; }
    public decimal? CurrentPortfolioValue { get; set; }

    public string AlertType { get; set; } = string.Empty;
    public decimal ThresholdValue { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? TriggeredAt { get; set; }
    public string? Message { get; set; }

    public string Status => TriggeredAt.HasValue
        ? "TRIGGERED"
        : (IsActive ? "ACTIVE" : "DISABLED");
}
