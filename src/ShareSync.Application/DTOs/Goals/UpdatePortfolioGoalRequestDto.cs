namespace ShareSync.Application.DTOs.Goals;

public class UpdatePortfolioGoalRequestDto
{
    public decimal TargetValue { get; set; }
    public DateTime? TargetDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}
