namespace ShareSync.Domain.Entities;

public class Alert
{
    public int AlertId { get; set; }
    public int UserId { get; set; }
    public int? CompanyId { get; set; }
    public int? PortfolioId { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public decimal ThresholdValue { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TriggeredAt { get; set; }
    public string? Message { get; set; }

    public AppUser? User { get; set; }
    public Company? Company { get; set; }
    public Portfolio? Portfolio { get; set; }
}
