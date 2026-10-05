namespace ShareSync.Application.DTOs.Goals;

public class CreatePortfolioGoalRequestDto
{
    public int PortfolioId { get; set; }
    public string GoalType { get; set; } = "TARGET_PORTFOLIO_VALUE"; // TARGET_PORTFOLIO_VALUE, TARGET_RETURN, TARGET_DIVIDEND_INCOME
    public decimal TargetValue { get; set; }
    public DateTime? TargetDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
