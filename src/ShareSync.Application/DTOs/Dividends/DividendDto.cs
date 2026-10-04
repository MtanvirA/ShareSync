namespace ShareSync.Application.DTOs.Dividends;

public class DividendDto
{
    public int DividendId { get; set; }
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public decimal DividendPerShare { get; set; }
    public DateTime DeclarationDate { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Status { get; set; } = "Paid";
    public decimal UserSharesHeld { get; set; }
    public decimal EstimatedIncome { get; set; }
}
