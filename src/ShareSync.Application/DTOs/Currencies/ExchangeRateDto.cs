namespace ShareSync.Application.DTOs.Currencies;

public class ExchangeRateDto
{
    public int RateId { get; set; }
    public string FromCurrency { get; set; } = "USD";
    public string ToCurrency { get; set; } = "BDT";
    public decimal ExchangeRate { get; set; }
    public decimal Rate => ExchangeRate;
    public DateTime EffectiveDate { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class CurrencyConversionResultDto
{
    public decimal OriginalAmount { get; set; }
    public string FromCurrency { get; set; } = "USD";
    public decimal ConvertedAmount { get; set; }
    public string ToCurrency { get; set; } = "BDT";
    public decimal ExchangeRate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class SupportedCurrenciesAndAssetsDto
{
    public List<string> SupportedCurrencies { get; set; } = new();
    public string DefaultBaseCurrency { get; set; } = "BDT";
    public List<string> SupportedAssetTypes { get; set; } = new();
}
