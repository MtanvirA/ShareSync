namespace ShareSync.Domain.Entities;

public class TransactionAudit
{
    public int AuditId { get; set; }
    public int? TransactionId { get; set; }
    public string ActionType { get; set; } = string.Empty; // "INSERT", "UPDATE", "DELETE"
    public DateTime ActionDate { get; set; } = DateTime.UtcNow;
    public string? ChangedBy { get; set; }
    public string? Details { get; set; }

    public Transaction? Transaction { get; set; }
}
