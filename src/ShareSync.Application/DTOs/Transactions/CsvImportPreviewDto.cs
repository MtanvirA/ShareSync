namespace ShareSync.Application.DTOs.Transactions;

public class CsvImportPreviewDto
{
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ValidRowsCount { get; set; }
    public int InvalidRowsCount { get; set; }
    public decimal TotalEstimatedAmount { get; set; }
    public bool IsValid => InvalidRowsCount == 0 && TotalRows > 0;
    public List<CsvTransactionRowPreviewDto> Rows { get; set; } = new();
    public List<CsvRowErrorDto> Errors { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}
