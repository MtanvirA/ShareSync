namespace ShareSync.Application.DTOs.Goals;

public class PortfolioGoalDto
{
    public int GoalId { get; set; }
    public int UserId { get; set; }
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public string GoalType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TargetValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal ProgressPercentage { get; set; }
    public decimal RemainingValue { get; set; }
    public DateTime? TargetDate { get; set; }
    public string Status { get; set; } = "ON_TRACK"; // ON_TRACK, AT_RISK, ACHIEVED
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
