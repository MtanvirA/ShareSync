namespace ShareSync.Application.DTOs.Transactions;

public class PagedTransactionsDto
{
    public List<TransactionDto> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}
