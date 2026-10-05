namespace ShareSync.Application.DTOs.Transactions;

public class CsvTransactionRowPreviewDto
{
    public int RowNumber { get; set; }
    public DateTime? TransactionDate { get; set; }
    public string DateString { get; set; } = string.Empty;
    public string CompanyTicker { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public int? CompanyId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal PricePerShare { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public bool IsValid { get; set; }
    public List<CsvRowErrorDto> Errors { get; set; } = new();
}
