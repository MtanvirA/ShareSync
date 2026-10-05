using Microsoft.EntityFrameworkCore;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<Sector> Sectors { get; }
    DbSet<Company> Companies { get; }
    DbSet<Portfolio> Portfolios { get; }
    DbSet<Watchlist> Watchlists { get; }
    DbSet<WatchlistItem> WatchlistItems { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<Dividend> Dividends { get; }
    DbSet<TransactionAudit> TransactionAudits { get; }
    DbSet<PortfolioSnapshot> PortfolioSnapshots { get; }
    DbSet<CompanyPriceHistory> CompanyPriceHistories { get; }
    DbSet<Alert> Alerts { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<PortfolioGoal> PortfolioGoals { get; }
    DbSet<PortfolioHoldingView> PortfolioHoldings { get; }

    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
