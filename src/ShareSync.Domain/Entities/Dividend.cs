namespace ShareSync.Domain.Entities;

public class Dividend
{
    public int DividendId { get; set; }
    public int CompanyId { get; set; }
    public decimal DividendPerShare { get; set; }
    public DateTime DeclarationDate { get; set; }
    public DateTime PaymentDate { get; set; }

    public Company? Company { get; set; }
}
