namespace ShareSync.Application.DTOs.Transactions;

public class CsvImportResultDto
{
    public string ImportId { get; set; } = Guid.NewGuid().ToString("N");
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string ImportMode { get; set; } = "Atomic";
    public List<TransactionDto> ImportedTransactions { get; set; } = new();
    public List<CsvRowErrorDto> SkippedRows { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
