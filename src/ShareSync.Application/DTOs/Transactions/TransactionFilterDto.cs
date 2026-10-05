namespace ShareSync.Application.DTOs.Transactions;

public class TransactionFilterDto
{
    public int? PortfolioId { get; set; }
    public int? CompanyId { get; set; }
    public string? TransactionType { get; set; } // "BUY", "SELL", "all"
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public string? SortBy { get; set; } = "date"; // "date", "quantity", "price", "value"
    public string? SortDirection { get; set; } = "desc"; // "asc", "desc"
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
