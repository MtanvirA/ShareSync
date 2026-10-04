namespace ShareSync.Application.DTOs.Reports;

public class TransactionHistoryReportItemDto
{
    public int TransactionId { get; set; }
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty; // BUY / SELL
    public decimal Quantity { get; set; }
    public decimal PricePerShare { get; set; }
    public decimal TotalTransactionValue { get; set; }
    public DateTime TransactionDate { get; set; }
}

public class TransactionHistoryReportDto
{
    public int TotalTransactions { get; set; }
    public int BuyCount { get; set; }
    public int SellCount { get; set; }
    public decimal TotalBuyValue { get; set; }
    public decimal TotalSellValue { get; set; }
    public decimal NetCashFlow { get; set; }
    public List<TransactionHistoryReportItemDto> Transactions { get; set; } = new();
}
