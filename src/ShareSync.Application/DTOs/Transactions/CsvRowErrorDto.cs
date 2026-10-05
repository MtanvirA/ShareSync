namespace ShareSync.Application.DTOs.Transactions;

public class CsvRowErrorDto
{
    public int RowNumber { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Problem { get; set; } = string.Empty;
    public string SuggestedCorrection { get; set; } = string.Empty;
}
