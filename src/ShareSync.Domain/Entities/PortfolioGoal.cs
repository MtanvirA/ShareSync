namespace ShareSync.Domain.Entities;

public class PortfolioGoal
{
    public int GoalId { get; set; }
    public int UserId { get; set; }
    public int PortfolioId { get; set; }
    public string GoalType { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public DateTime? TargetDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public AppUser? User { get; set; }
    public Portfolio? Portfolio { get; set; }
}
