namespace ShareSync.Domain.Entities;

public class CurrencyExchangeRate
{
    public int RateId { get; set; }
    public string FromCurrency { get; set; } = "USD";
    public string ToCurrency { get; set; } = "BDT";
    public decimal ExchangeRate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
