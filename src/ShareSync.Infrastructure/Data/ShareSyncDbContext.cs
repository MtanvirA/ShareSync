using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Infrastructure.Data;

public class ShareSyncDbContext : DbContext, IApplicationDbContext
{
    public ShareSyncDbContext(DbContextOptions<ShareSyncDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Watchlist> Watchlists => Set<Watchlist>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Dividend> Dividends => Set<Dividend>();
    public DbSet<TransactionAudit> TransactionAudits => Set<TransactionAudit>();
    public DbSet<PortfolioSnapshot> PortfolioSnapshots => Set<PortfolioSnapshot>();
    public DbSet<CompanyPriceHistory> CompanyPriceHistories => Set<CompanyPriceHistory>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PortfolioGoal> PortfolioGoals => Set<PortfolioGoal>();
    public DbSet<PortfolioHoldingView> PortfolioHoldings => Set<PortfolioHoldingView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. APP_USERS
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("APP_USERS");
            entity.HasKey(e => e.UserId).HasName("PK_APP_USERS");

            entity.Property(e => e.UserId)
                .HasColumnName("USER_ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Name)
                .HasColumnName("NAME")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Email)
                .HasColumnName("EMAIL")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.PasswordHash)
                .HasColumnName("PASSWORD_HASH")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Role)
                .HasColumnName("ROLE")
                .HasMaxLength(20)
                .HasDefaultValue("INVESTOR")
                .IsRequired();

            entity.Property(e => e.IsActive)
                .HasColumnName("IS_ACTIVE")
                .HasDefaultValue(true)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CREATED_AT")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.HasIndex(e => e.Email, "UQ_APP_USERS_EMAIL").IsUnique();
        });

        // 2. SECTORS
        modelBuilder.Entity<Sector>(entity =>
        {
            entity.ToTable("SECTORS");
            entity.HasKey(e => e.SectorId).HasName("PK_SECTORS");

            entity.Property(e => e.SectorId)
                .HasColumnName("SECTOR_ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.SectorName)
                .HasColumnName("SECTOR_NAME")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("DESCRIPTION")
                .HasMaxLength(255);

            entity.HasIndex(e => e.SectorName, "UQ_SECTORS_NAME").IsUnique();
        });

        // 3. COMPANIES
        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("COMPANIES", t =>
            {
                t.HasCheckConstraint("CK_COMPANIES_PRICE", "CURRENT_PRICE > 0");
                t.HasCheckConstraint("CK_COMPANIES_MARKET_CAP", "MARKET_CAP IS NULL OR MARKET_CAP >= 0");
            });
            entity.HasKey(e => e.CompanyId).HasName("PK_COMPANIES");

            entity.Property(e => e.CompanyId)
                .HasColumnName("COMPANY_ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.CompanyName)
                .HasColumnName("COMPANY_NAME")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.TickerSymbol)
                .HasColumnName("TICKER_SYMBOL")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.SectorId)
                .HasColumnName("SECTOR_ID")
                .IsRequired();

            entity.Property(e => e.CurrentPrice)
                .HasColumnName("CURRENT_PRICE")
                .HasPrecision(14, 2)
                .IsRequired();

            entity.Property(e => e.MarketCap)
                .HasColumnName("MARKET_CAP")
                .HasPrecision(20, 2);

            entity.Property(e => e.IsActive)
                .HasColumnName("IS_ACTIVE")
                .HasDefaultValue(true)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CREATED_AT")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.HasIndex(e => e.TickerSymbol, "UQ_COMPANIES_TICKER").IsUnique();

            entity.HasOne(e => e.Sector)
                .WithMany(s => s.Companies)
                .HasForeignKey(e => e.SectorId)
                .HasConstraintName("FK_COMPANIES_SECTOR")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 4. PORTFOLIOS
        modelBuilder.Entity<Portfolio>(entity =>
        {
            entity.ToTable("PORTFOLIOS");
            entity.HasKey(e => e.PortfolioId).HasName("PK_PORTFOLIOS");

            entity.Property(e => e.PortfolioId)
                .HasColumnName("PORTFOLIO_ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.UserId)
                .HasColumnName("USER_ID")
                .IsRequired();

            entity.Property(e => e.PortfolioName)
                .HasColumnName("PORTFOLIO_NAME")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("DESCRIPTION")
                .HasMaxLength(255);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CREATED_AT")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.HasIndex(e => new { e.UserId, e.PortfolioName }, "UQ_PORTFOLIOS_USER_NAME").IsUnique();

            entity.HasOne(e => e.User)
                .WithMany(u => u.Portfolios)
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("FK_PORTFOLIOS_USER")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 5. WATCHLISTS
        modelBuilder.Entity<Watchlist>(entity =>
        {
            entity.ToTable("WATCHLISTS");
            entity.HasKey(e => e.WatchlistId).HasName("PK_WATCHLISTS");

            entity.Property(e => e.WatchlistId)
                .HasColumnName("WATCHLIST_ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.UserId)
                .HasColumnName("USER_ID")
                .IsRequired();

            entity.Property(e => e.WatchlistName)
                .HasColumnName("WATCHLIST_NAME")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("DESCRIPTION")
                .HasMaxLength(255);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CREATED_AT")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.HasIndex(e => new { e.UserId, e.WatchlistName }, "UQ_WATCHLISTS_USER_NAME").IsUnique();

            entity.HasOne(e => e.User)
                .WithMany(u => u.Watchlists)
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("FK_WATCHLISTS_USER")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 6. WATCHLIST_ITEMS
        modelBuilder.Entity<WatchlistItem>(entity =>
        {
            entity.ToTable("WATCHLIST_ITEMS", t =>
            {
                t.HasCheckConstraint("CK_WATCHLIST_ITEMS_TARGET", "TARGET_PRICE IS NULL OR TARGET_PRICE > 0");
            });
            entity.HasKey(e => new { e.WatchlistId, e.CompanyId }).HasName("PK_WATCHLIST_ITEMS");

            entity.Property(e => e.WatchlistId).HasColumnName("WATCHLIST_ID");
            entity.Property(e => e.CompanyId).HasColumnName("COMPANY_ID");

            entity.Property(e => e.TargetPrice)
                .HasColumnName("TARGET_PRICE")
                .HasPrecision(14, 2);

            entity.Property(e => e.AddedAt)
                .HasColumnName("ADDED_AT")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.HasOne(e => e.Watchlist)
                .WithMany(w => w.Items)
                .HasForeignKey(e => e.WatchlistId)
                .HasConstraintName("FK_WATCHLIST_ITEMS_WATCHLIST")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Company)
                .WithMany(c => c.WatchlistItems)
                .HasForeignKey(e => e.CompanyId)
                .HasConstraintName("FK_WATCHLIST_ITEMS_COMPANY")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 7. TRANSACTIONS
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("TRANSACTIONS", t =>
            {
                t.HasCheckConstraint("CK_TRANSACTIONS_TYPE", "TRANSACTION_TYPE IN ('BUY', 'SELL')");
                t.HasCheckConstraint("CK_TRANSACTIONS_QUANTITY", "QUANTITY > 0");
                t.HasCheckConstraint("CK_TRANSACTIONS_PRICE", "PRICE_PER_SHARE > 0");
            });
            entity.HasKey(e => e.TransactionId).HasName("PK_TRANSACTIONS");

            entity.Property(e => e.TransactionId)
                .HasColumnName("TRANSACTION_ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.PortfolioId)
                .HasColumnName("PORTFOLIO_ID")
                .IsRequired();

            entity.Property(e => e.CompanyId)
                .HasColumnName("COMPANY_ID")
                .IsRequired();

            entity.Property(e => e.TransactionType)
                .HasColumnName("TRANSACTION_TYPE")
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(e => e.Quantity)
                .HasColumnName("QUANTITY")
                .HasPrecision(14, 4)
                .IsRequired();

            entity.Property(e => e.PricePerShare)
                .HasColumnName("PRICE_PER_SHARE")
                .HasPrecision(14, 2)
                .IsRequired();

            entity.Property(e => e.TransactionDate)
                .HasColumnName("TRANSACTION_DATE")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.HasOne(e => e.Portfolio)
                .WithMany(p => p.Transactions)
                .HasForeignKey(e => e.PortfolioId)
                .HasConstraintName("FK_TRANSACTIONS_PORTFOLIO")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Company)
                .WithMany(c => c.Transactions)
                .HasForeignKey(e => e.CompanyId)
                .HasConstraintName("FK_TRANSACTIONS_COMPANY")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 8. DIVIDENDS
        modelBuilder.Entity<Dividend>(entity =>
        {
            entity.ToTable("DIVIDENDS", t =>
            {
                t.HasCheckConstraint("CK_DIVIDENDS_AMOUNT", "DIVIDEND_PER_SHARE > 0");
                t.HasCheckConstraint("CK_DIVIDENDS_DATES", "PAYMENT_DATE >= DECLARATION_DATE");
            });
            entity.HasKey(e => e.DividendId).HasName("PK_DIVIDENDS");

            entity.Property(e => e.DividendId)
                .HasColumnName("DIVIDEND_ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.CompanyId)
                .HasColumnName("COMPANY_ID")
                .IsRequired();

            entity.Property(e => e.DividendPerShare)
                .HasColumnName("DIVIDEND_PER_SHARE")
                .HasPrecision(14, 2)
                .IsRequired();

            entity.Property(e => e.DeclarationDate)
                .HasColumnName("DECLARATION_DATE")
                .HasColumnType("DATE")
                .IsRequired();

            entity.Property(e => e.PaymentDate)
                .HasColumnName("PAYMENT_DATE")
                .HasColumnType("DATE")
                .IsRequired();

            entity.HasOne(e => e.Company)
                .WithMany(c => c.Dividends)
                .HasForeignKey(e => e.CompanyId)
                .HasConstraintName("FK_DIVIDENDS_COMPANY")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 9. TRANSACTION_AUDIT
        modelBuilder.Entity<TransactionAudit>(entity =>
        {
            entity.ToTable("TRANSACTION_AUDIT");
            entity.HasKey(e => e.AuditId).HasName("PK_TRANSACTION_AUDIT");

            entity.Property(e => e.AuditId)
                .HasColumnName("AUDIT_ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.TransactionId)
                .HasColumnName("TRANSACTION_ID");

            entity.Property(e => e.ActionType)
                .HasColumnName("ACTION_TYPE")
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(e => e.ActionDate)
                .HasColumnName("ACTION_DATE")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.ChangedBy)
                .HasColumnName("CHANGED_BY")
                .HasMaxLength(100);

            entity.Property(e => e.Details)
                .HasColumnName("DETAILS")
                .HasMaxLength(1000);

            entity.HasOne(e => e.Transaction)
                .WithMany(t => t.Audits)
                .HasForeignKey(e => e.TransactionId)
                .HasConstraintName("FK_TRANSACTION_AUDIT_TRANSACTION")
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 10. PORTFOLIO_SNAPSHOTS
        modelBuilder.Entity<PortfolioSnapshot>(entity =>
        {
            entity.ToTable("PORTFOLIO_SNAPSHOTS", t =>
            {
                t.HasCheckConstraint("CK_PORTFOLIO_SNAPSHOT_VALUE", "TOTAL_VALUE >= 0");
            });
            entity.HasKey(e => e.SnapshotId).HasName("PK_PORTFOLIO_SNAPSHOTS");

            entity.Property(e => e.SnapshotId)
                .HasColumnName("SNAPSHOT_ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.PortfolioId)
                .HasColumnName("PORTFOLIO_ID")
                .IsRequired();

            entity.Property(e => e.SnapshotDate)
                .HasColumnName("SNAPSHOT_DATE")
                .HasColumnType("DATE")
                .IsRequired();

            entity.Property(e => e.TotalValue)
                .HasColumnName("TOTAL_VALUE")
                .HasPrecision(20, 2)
                .IsRequired();

            entity.HasIndex(e => new { e.PortfolioId, e.SnapshotDate }, "UQ_PORTFOLIO_SNAPSHOT_DATE").IsUnique();

            entity.HasOne(e => e.Portfolio)
                .WithMany(p => p.Snapshots)
                .HasForeignKey(e => e.PortfolioId)
                .HasConstraintName("FK_PORTFOLIO_SNAPSHOTS_PORTFOLIO")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 11. COMPANY_PRICE_HISTORY
        modelBuilder.Entity<CompanyPriceHistory>(entity =>
        {
            entity.ToTable("COMPANY_PRICE_HISTORY", t =>
            {
                t.HasCheckConstraint("CK_PRICE_HIST_PRICE", "PRICE > 0");
                t.HasCheckConstraint("CK_PRICE_HIST_OPEN", "OPEN_PRICE IS NULL OR OPEN_PRICE > 0");
                t.HasCheckConstraint("CK_PRICE_HIST_HIGH", "HIGH_PRICE IS NULL OR HIGH_PRICE > 0");
                t.HasCheckConstraint("CK_PRICE_HIST_LOW", "LOW_PRICE IS NULL OR LOW_PRICE > 0");
                t.HasCheckConstraint("CK_PRICE_HIST_VOLUME", "VOLUME IS NULL OR VOLUME >= 0");
            });

            entity.HasKey(e => e.PriceHistoryId).HasName("PK_COMPANY_PRICE_HISTORY");

            entity.Property(e => e.PriceHistoryId)
                .HasColumnName("PRICE_HISTORY_ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.CompanyId)
                .HasColumnName("COMPANY_ID")
                .IsRequired();

            entity.Property(e => e.Price)
                .HasColumnName("PRICE")
                .HasPrecision(14, 2)
                .IsRequired();

            entity.Property(e => e.OpenPrice)
                .HasColumnName("OPEN_PRICE")
                .HasPrecision(14, 2);

            entity.Property(e => e.HighPrice)
                .HasColumnName("HIGH_PRICE")
                .HasPrecision(14, 2);

            entity.Property(e => e.LowPrice)
                .HasColumnName("LOW_PRICE")
                .HasPrecision(14, 2);

            entity.Property(e => e.Volume)
                .HasColumnName("VOLUME");

            entity.Property(e => e.RecordedAt)
                .HasColumnName("RECORDED_AT")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CREATED_AT")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.TradingDate)
                .HasColumnName("TRADING_DATE")
                .HasColumnType("DATE");

            entity.Property(e => e.Source)
                .HasColumnName("SOURCE")
                .HasMaxLength(100);

            entity.Property(e => e.SourceDataset)
                .HasColumnName("SOURCE_DATASET")
                .HasMaxLength(255);

            entity.Property(e => e.SourceDoi)
                .HasColumnName("SOURCE_DOI")
                .HasMaxLength(100);

            entity.Property(e => e.ImportBatchId)
                .HasColumnName("IMPORT_BATCH_ID")
                .HasMaxLength(100);

            entity.HasIndex(e => new { e.CompanyId, e.RecordedAt }, "IDX_PRICE_HIST_COMP_DATE");
            entity.HasIndex(e => new { e.CompanyId, e.TradingDate }, "UQ_PRICE_HIST_COMP_TRADING_DATE").IsUnique();

            entity.HasOne(e => e.Company)
                .WithMany(c => c.PriceHistories)
                .HasForeignKey(e => e.CompanyId)
                .HasConstraintName("FK_PRICE_HISTORY_COMPANY")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 13. ALERTS
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.ToTable("ALERTS", t =>
            {
                t.HasCheckConstraint("CK_ALERTS_TYPE", "ALERT_TYPE IN ('PRICE_ABOVE', 'PRICE_BELOW', 'PORTFOLIO_VALUE_ABOVE', 'PORTFOLIO_VALUE_BELOW')");
                t.HasCheckConstraint("CK_ALERTS_THRESHOLD", "THRESHOLD_VALUE > 0");
                t.HasCheckConstraint("CK_ALERTS_IS_ACTIVE", "IS_ACTIVE IN (0, 1)");
            });
            entity.HasKey(e => e.AlertId).HasName("PK_ALERTS");

            entity.Property(e => e.AlertId)
                .HasColumnName("ALERT_ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.UserId)
                .HasColumnName("USER_ID")
                .IsRequired();

            entity.Property(e => e.CompanyId)
                .HasColumnName("COMPANY_ID");

            entity.Property(e => e.PortfolioId)
                .HasColumnName("PORTFOLIO_ID");

            entity.Property(e => e.AlertType)
                .HasColumnName("ALERT_TYPE")
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(e => e.ThresholdValue)
                .HasColumnName("THRESHOLD_VALUE")
                .HasPrecision(14, 2)
                .IsRequired();

            entity.Property(e => e.IsActive)
                .HasColumnName("IS_ACTIVE")
                .HasConversion<int>()
                .HasDefaultValue(true)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CREATED_AT")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.TriggeredAt)
                .HasColumnName("TRIGGERED_AT");

            entity.Property(e => e.Message)
                .HasColumnName("MESSAGE")
                .HasMaxLength(500);

            entity.HasIndex(e => new { e.UserId, e.IsActive }, "IDX_ALERTS_USER");
            entity.HasIndex(e => new { e.IsActive, e.CompanyId, e.AlertType }, "IDX_ALERTS_PRICE_EVAL");
            entity.HasIndex(e => new { e.IsActive, e.PortfolioId, e.AlertType }, "IDX_ALERTS_PORT_EVAL");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("FK_ALERTS_USER")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .HasConstraintName("FK_ALERTS_COMPANY")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Portfolio)
                .WithMany()
                .HasForeignKey(e => e.PortfolioId)
                .HasConstraintName("FK_ALERTS_PORTFOLIO")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 13. NOTIFICATIONS
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("NOTIFICATIONS");
            entity.HasKey(e => e.NotificationId).HasName("PK_NOTIFICATIONS");

            entity.Property(e => e.NotificationId)
                .HasColumnName("NOTIFICATION_ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.UserId)
                .HasColumnName("USER_ID")
                .IsRequired();

            entity.Property(e => e.NotificationType)
                .HasColumnName("NOTIFICATION_TYPE")
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(e => e.Title)
                .HasColumnName("TITLE")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.Message)
                .HasColumnName("MESSAGE")
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(e => e.IsRead)
                .HasColumnName("IS_READ")
                .HasConversion<int>()
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CREATED_AT")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.RelatedEntityType)
                .HasColumnName("RELATED_ENTITY_TYPE")
                .HasMaxLength(50);

            entity.Property(e => e.RelatedEntityId)
                .HasColumnName("RELATED_ENTITY_ID");

            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt }, "IDX_NOTIFICATIONS_USER");
            entity.HasIndex(e => new { e.UserId, e.RelatedEntityType, e.RelatedEntityId }, "IDX_NOTIFICATIONS_ENTITY");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("FK_NOTIFICATIONS_USER")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 14. PORTFOLIO_GOALS
        modelBuilder.Entity<PortfolioGoal>(entity =>
        {
            entity.ToTable("PORTFOLIO_GOALS");
            entity.HasKey(e => e.GoalId).HasName("PK_PORTFOLIO_GOALS");

            entity.Property(e => e.GoalId)
                .HasColumnName("GOAL_ID")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.UserId)
                .HasColumnName("USER_ID")
                .IsRequired();

            entity.Property(e => e.PortfolioId)
                .HasColumnName("PORTFOLIO_ID")
                .IsRequired();

            entity.Property(e => e.GoalType)
                .HasColumnName("GOAL_TYPE")
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(e => e.TargetValue)
                .HasColumnName("TARGET_VALUE")
                .HasPrecision(15, 2)
                .IsRequired();

            entity.Property(e => e.TargetDate)
                .HasColumnName("TARGET_DATE");

            entity.Property(e => e.Title)
                .HasColumnName("TITLE")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("DESCRIPTION")
                .HasMaxLength(500);

            entity.Property(e => e.IsActive)
                .HasColumnName("IS_ACTIVE")
                .HasConversion<int>()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CREATED_AT")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("UPDATED_AT");

            entity.HasIndex(e => new { e.UserId, e.IsActive, e.CreatedAt }, "IDX_PORTFOLIO_GOALS_USER");
            entity.HasIndex(e => new { e.PortfolioId, e.IsActive }, "IDX_PORTFOLIO_GOALS_PORTFOLIO");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("FK_PORTFOLIO_GOALS_USER")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Portfolio)
                .WithMany()
                .HasForeignKey(e => e.PortfolioId)
                .HasConstraintName("FK_PORTFOLIO_GOALS_PORTFOLIO")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // VIEW: VW_PORTFOLIO_HOLDINGS
        modelBuilder.Entity<PortfolioHoldingView>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("VW_PORTFOLIO_HOLDINGS");

            entity.Property(e => e.PortfolioId).HasColumnName("PORTFOLIO_ID");
            entity.Property(e => e.PortfolioName).HasColumnName("PORTFOLIO_NAME");
            entity.Property(e => e.CompanyId).HasColumnName("COMPANY_ID");
            entity.Property(e => e.CompanyName).HasColumnName("COMPANY_NAME");
            entity.Property(e => e.TickerSymbol).HasColumnName("TICKER_SYMBOL");
            entity.Property(e => e.CurrentQuantity).HasColumnName("CURRENT_QUANTITY").HasPrecision(14, 4);
            entity.Property(e => e.CurrentPrice).HasColumnName("CURRENT_PRICE").HasPrecision(14, 2);
            entity.Property(e => e.CurrentMarketValue).HasColumnName("CURRENT_MARKET_VALUE").HasPrecision(20, 2);
            entity.Property(e => e.WeightedAverageBuyPrice).HasColumnName("WEIGHTED_AVERAGE_BUY_PRICE").HasPrecision(14, 2);
            entity.Property(e => e.UnrealizedProfitLoss).HasColumnName("UNREALIZED_PROFIT_LOSS").HasPrecision(20, 2);
        });
    }
}
