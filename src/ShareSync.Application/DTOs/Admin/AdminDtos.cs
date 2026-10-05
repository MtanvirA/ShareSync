namespace ShareSync.Application.DTOs.Admin;

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalCompanies { get; set; }
    public int ActiveCompanies { get; set; }
    public int TotalSectors { get; set; }
    public int TotalPortfolios { get; set; }
    public int TotalTransactions { get; set; }
    public decimal TotalTransactionVolume { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public int TotalPriceHistoryRecords { get; set; }
    public string SystemStatus { get; set; } = "OPERATIONAL";
    public string DatabaseHealth { get; set; } = "HEALTHY";
}

public class SystemDiagnosticsDto
{
    // SYSTEM STATUS
    public string DatabaseStatus { get; set; } = "Connected";
    public bool IsDatabaseConnected { get; set; } = true;
    public string ApplicationStatus { get; set; } = "Healthy";
    public string DseSyncStatus { get; set; } = "Running";

    // DATABASE (aggregate counts)
    public int UsersCount { get; set; }
    public int CompaniesCount { get; set; }
    public int PortfoliosCount { get; set; }
    public int TransactionsCount { get; set; }
    public int AuditRecordsCount { get; set; }

    // SYNCHRONIZATION
    public DateTime? LastSyncTime { get; set; }
    public string SyncStatus { get; set; } = "Successful";
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
}

public class AdminCompanyDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public int SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal? MarketCap { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TransactionCount { get; set; }
    public int WatchlistCount { get; set; }
}

public class CreateCompanyRequestDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public int SectorId { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal? MarketCap { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateCompanyRequestDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string TickerSymbol { get; set; } = string.Empty;
    public int SectorId { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal? MarketCap { get; set; }
    public bool IsActive { get; set; }
}

public class AdminSectorDto
{
    public int SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CompaniesCount { get; set; }
    public decimal TotalMarketCap { get; set; }
}

public class CreateSectorRequestDto
{
    public string SectorName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateSectorRequestDto
{
    public string SectorName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AdminUserDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "INVESTOR";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int PortfolioCount { get; set; }
    public int TransactionCount { get; set; }
}

public class UpdateUserRoleRequestDto
{
    public string Role { get; set; } = "INVESTOR";
}

public class AdminAuditLogDto
{
    public int AuditId { get; set; }
    public int? TransactionId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public DateTime ActionDate { get; set; }
    public string? ChangedBy { get; set; }
    public string? Details { get; set; }
    public string? PortfolioName { get; set; }
    public string? TickerSymbol { get; set; }
}
