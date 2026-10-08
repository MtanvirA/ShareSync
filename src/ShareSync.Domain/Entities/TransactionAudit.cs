namespace ShareSync.Domain.Entities;

/// <summary>
/// AUDIT: Transaction Audit Trail
/// DATABASE: Table — transaction_audit
/// DATABASE: Trigger — trg_transactions_after_*
/// 
/// PURPOSE:
/// Immutable audit log capturing all lifecycle actions (INSERT, UPDATE, DELETE) executed
/// against the transactions ledger. Captured atomically by MySQL database triggers.
/// On DELETE, the trigger preserves the historical audit row by setting TransactionId = NULL
/// while recording the full transaction metadata in Details.
/// 
/// RELATIONAL MAPPING:
/// - Primary Key: AuditId (AUTO_INCREMENT in MySQL)
/// - Foreign Key: TransactionId -> transactions(transaction_id) ON DELETE SET NULL
/// </summary>
public class TransactionAudit
{
    /// <summary>
    /// DATABASE: Primary key identifier for the audit entry.
    /// </summary>
    public int AuditId { get; set; }

    /// <summary>
    /// Foreign key to parent transaction. Nullable to retain historical records if transaction is deleted.
    /// </summary>
    public int? TransactionId { get; set; }

    /// <summary>
    /// AUDIT: Action verb: "INSERT", "UPDATE", or "DELETE".
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of database modification.
    /// </summary>
    public DateTime ActionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User identifier or database session user that executed the change.
    /// </summary>
    public string? ChangedBy { get; set; }

    /// <summary>
    /// Detailed JSON or formatted string of pre/post order attributes captured by trigger.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Navigation property to audited transaction (null if transaction was deleted).
    /// </summary>
    public Transaction? Transaction { get; set; }
}

