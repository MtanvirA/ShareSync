using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShareSync.Domain.Entities;

namespace ShareSync.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(ShareSyncDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Ensuring Oracle database schema is created...");
            await context.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation("Oracle database schema verified successfully.");

            // Create view if it does not exist
            await EnsureViewsAsync(context, logger, cancellationToken);

            // Seed reference data if empty
            await SeedAsync(context, logger, cancellationToken);

            // Ensure seeded user password hash is valid PBKDF2 hash
            var seedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "tanvir@sharesync.com", cancellationToken);
            if (seedUser != null && !seedUser.PasswordHash.Contains('.'))
            {
                seedUser.PasswordHash = new Security.PasswordHasher().HashPassword("Password123#");
                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Updated seed user password hash.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the Oracle database.");
            throw;
        }
    }

    private static async Task EnsureViewsAsync(ShareSyncDbContext context, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var createViewSql = @"
CREATE OR REPLACE VIEW VW_PORTFOLIO_HOLDINGS AS
SELECT
    t.portfolio_id,
    p.portfolio_name,
    t.company_id,
    c.company_name,
    c.ticker_symbol,
    SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) AS current_quantity,
    c.current_price,
    SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) * c.current_price AS current_market_value,
    CASE
        WHEN SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END) > 0
        THEN SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity * t.price_per_share ELSE 0 END) / SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END)
        ELSE 0
    END AS weighted_average_buy_price,
    (
        SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) * c.current_price
    ) - (
        SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) *
        CASE
            WHEN SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END) > 0
            THEN SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity * t.price_per_share ELSE 0 END) / SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END)
            ELSE 0
        END
    ) AS unrealized_profit_loss
FROM transactions t
JOIN portfolios p ON t.portfolio_id = p.portfolio_id
JOIN companies c ON t.company_id = c.company_id
GROUP BY t.portfolio_id, p.portfolio_name, t.company_id, c.company_name, c.ticker_symbol, c.current_price
HAVING SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) > 0";

            await context.Database.ExecuteSqlRawAsync(createViewSql, cancellationToken);
            logger.LogInformation("VW_PORTFOLIO_HOLDINGS view verified.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure VW_PORTFOLIO_HOLDINGS view. Continuing...");
        }
    }

    private static async Task SeedAsync(ShareSyncDbContext context, ILogger logger, CancellationToken cancellationToken)
    {
        if (await context.Sectors.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Database already contains data. Skipping initial seeding.");
            return;
        }

        logger.LogInformation("Seeding initial reference data...");

        var hasher = new Security.PasswordHasher();
        var user = new AppUser
        {
            Name = "Tanvir Anjum",
            Email = "tanvir@sharesync.com",
            PasswordHash = hasher.HashPassword("Password123#"),
            Role = "INVESTOR",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        // 2. Sectors
        var tech = new Sector { SectorName = "Telecommunication", Description = "Telecommunication and mobile network services" };
        var pharma = new Sector { SectorName = "Pharmaceuticals", Description = "Pharmaceutical and healthcare companies" };
        var consumer = new Sector { SectorName = "Consumer Goods", Description = "Consumer products and FMCG companies" };
        var finance = new Sector { SectorName = "Financial Services", Description = "Banking, financial and investment services" };
        context.Sectors.AddRange(tech, pharma, consumer, finance);
        await context.SaveChangesAsync(cancellationToken);

        // 3. Companies
        var gp = new Company
        {
            CompanyName = "Grameenphone PLC",
            TickerSymbol = "GP",
            SectorId = tech.SectorId,
            CurrentPrice = 410.50m,
            MarketCap = 555000000000m,
            CreatedAt = DateTime.UtcNow
        };
        var beximco = new Company
        {
            CompanyName = "BEXIMCO Pharmaceuticals PLC",
            TickerSymbol = "BEXIMCO",
            SectorId = pharma.SectorId,
            CurrentPrice = 118.40m,
            MarketCap = 53500000000m,
            CreatedAt = DateTime.UtcNow
        };
        var batbc = new Company
        {
            CompanyName = "British American Tobacco Bangladesh",
            TickerSymbol = "BATBC",
            SectorId = consumer.SectorId,
            CurrentPrice = 462.75m,
            MarketCap = 250000000000m,
            CreatedAt = DateTime.UtcNow
        };
        var square = new Company
        {
            CompanyName = "Square Pharmaceuticals PLC",
            TickerSymbol = "SQURPHARMA",
            SectorId = pharma.SectorId,
            CurrentPrice = 226.80m,
            MarketCap = 200000000000m,
            CreatedAt = DateTime.UtcNow
        };
        context.Companies.AddRange(gp, beximco, batbc, square);
        await context.SaveChangesAsync(cancellationToken);

        // 4. Portfolio
        var portfolio = new Portfolio
        {
            UserId = user.UserId,
            PortfolioName = "Main Portfolio",
            Description = "Primary investment portfolio",
            CreatedAt = DateTime.UtcNow
        };
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync(cancellationToken);

        // 5. Watchlist
        var watchlist = new Watchlist
        {
            UserId = user.UserId,
            WatchlistName = "Primary Watchlist",
            Description = "High priority companies to monitor",
            CreatedAt = DateTime.UtcNow
        };
        context.Watchlists.Add(watchlist);
        await context.SaveChangesAsync(cancellationToken);

        context.WatchlistItems.AddRange(
            new WatchlistItem { WatchlistId = watchlist.WatchlistId, CompanyId = gp.CompanyId, TargetPrice = 390.00m, AddedAt = DateTime.UtcNow },
            new WatchlistItem { WatchlistId = watchlist.WatchlistId, CompanyId = beximco.CompanyId, TargetPrice = 110.00m, AddedAt = DateTime.UtcNow },
            new WatchlistItem { WatchlistId = watchlist.WatchlistId, CompanyId = square.CompanyId, TargetPrice = 215.00m, AddedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync(cancellationToken);

        // 6. Transactions
        var t1 = new Transaction
        {
            PortfolioId = portfolio.PortfolioId,
            CompanyId = beximco.CompanyId,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 120.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-30)
        };
        var t2 = new Transaction
        {
            PortfolioId = portfolio.PortfolioId,
            CompanyId = gp.CompanyId,
            TransactionType = "BUY",
            Quantity = 200,
            PricePerShare = 280.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-20)
        };
        var t3 = new Transaction
        {
            PortfolioId = portfolio.PortfolioId,
            CompanyId = batbc.CompanyId,
            TransactionType = "BUY",
            Quantity = 50,
            PricePerShare = 450.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-10)
        };
        context.Transactions.AddRange(t1, t2, t3);
        await context.SaveChangesAsync(cancellationToken);

        // 7. Dividends
        context.Dividends.AddRange(
            new Dividend
            {
                CompanyId = gp.CompanyId,
                DividendPerShare = 12.50m,
                DeclarationDate = DateTime.UtcNow.AddDays(-60),
                PaymentDate = DateTime.UtcNow.AddDays(-30)
            },
            new Dividend
            {
                CompanyId = beximco.CompanyId,
                DividendPerShare = 3.50m,
                DeclarationDate = DateTime.UtcNow.AddDays(-45),
                PaymentDate = DateTime.UtcNow.AddDays(-15)
            }
        );
        await context.SaveChangesAsync(cancellationToken);

        // 8. Snapshot
        context.PortfolioSnapshots.Add(new PortfolioSnapshot
        {
            PortfolioId = portfolio.PortfolioId,
            SnapshotDate = DateTime.UtcNow.Date,
            TotalValue = (100 * beximco.CurrentPrice) + (200 * gp.CurrentPrice) + (50 * batbc.CurrentPrice)
        });
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Database seeded successfully.");
    }
}
