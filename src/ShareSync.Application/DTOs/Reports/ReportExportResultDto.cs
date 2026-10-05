namespace ShareSync.Application.DTOs.Reports;

public class ReportExportResultDto
{
    public byte[] FileContents { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
