using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShareSync.Domain.Entities;

namespace ShareSync.Infrastructure.Data;

/// <summary>
/// Verifies database connectivity and performs safe, idempotent reference data seeding.
/// Does not alter database architecture or execute destructive DDL on existing schemas.
/// Authoritative schema deployments are managed via database/mysql/01_schema.sql.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(ShareSyncDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Verifying MySQL database connectivity and health...");

            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                logger.LogWarning("Unable to establish direct connection to MySQL database. Verifying connection string and network status.");
                return;
            }

            logger.LogInformation("MySQL database connection verified successfully.");

            // Idempotent data check: only seed if the database is completely empty
            await SeedReferenceDataIfEmptyAsync(context, logger, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while verifying the MySQL database.");
            throw;
        }
    }

    private static async Task SeedReferenceDataIfEmptyAsync(ShareSyncDbContext context, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {

            // Check if sectors already exist
            if (await context.Sectors.AnyAsync(cancellationToken))
            {
                logger.LogInformation("Database already contains initialized data. Schema and data verified.");
                if (!await context.Users.AnyAsync(u => u.Role == "ADMIN", cancellationToken))
                {
                    var h = new Security.PasswordHasher();
                    var admin = new AppUser
                    {
                        Name = "System Administrator",
                        Email = "admin@sharesync.com",
                        PasswordHash = h.HashPassword("Admin123#"),
                        Role = "ADMIN",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Users.Add(admin);
                    await context.SaveChangesAsync(cancellationToken);
                    logger.LogInformation("Admin user seeded: admin@sharesync.com");
                }

                if (!await context.Notifications.AnyAsync(cancellationToken))
                {
                    var investorUser = await context.Users.FirstOrDefaultAsync(u => u.Role == "INVESTOR", cancellationToken);
                    if (investorUser != null)
                    {
                        context.Notifications.Add(new Notification
                        {
                            UserId = investorUser.UserId,
                            NotificationType = "SYSTEM",
                            Title = "Welcome to ShareSync",
                            Message = "Your investment portfolio and real-time market tracker is ready.",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        });
                        await context.SaveChangesAsync(cancellationToken);
                        logger.LogInformation("Initial notification seeded for user #{UserId}", investorUser.UserId);
                    }
                }
                return;
            }

            logger.LogInformation("Database contains no sectors. Performing idempotent reference data seeding...");

            // 1. App Users (Investor and Admin)
            var hasher = new Security.PasswordHasher();
            var user = new AppUser
            {
                Name = "Tanvir Anjum",
                Email = "tanvir@sharesync.com",
                PasswordHash = hasher.HashPassword("Password123#"),
                Role = "INVESTOR",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var adminUser = new AppUser
            {
                Name = "System Administrator",
                Email = "admin@sharesync.com",
                PasswordHash = hasher.HashPassword("Admin123#"),
                Role = "ADMIN",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.AddRange(user, adminUser);
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

            // 9. Initial Baseline Price History
            var baseTime = DateTime.UtcNow;
            context.CompanyPriceHistories.AddRange(
                new CompanyPriceHistory { CompanyId = gp.CompanyId, Price = 400.00m, OpenPrice = 398.00m, HighPrice = 402.50m, LowPrice = 396.00m, Volume = 45000, RecordedAt = baseTime.AddDays(-30) },
                new CompanyPriceHistory { CompanyId = gp.CompanyId, Price = 405.50m, OpenPrice = 401.00m, HighPrice = 407.00m, LowPrice = 400.50m, Volume = 52000, RecordedAt = baseTime.AddDays(-20) },
                new CompanyPriceHistory { CompanyId = gp.CompanyId, Price = 408.00m, OpenPrice = 405.00m, HighPrice = 410.00m, LowPrice = 404.00m, Volume = 61000, RecordedAt = baseTime.AddDays(-10) },
                new CompanyPriceHistory { CompanyId = gp.CompanyId, Price = 410.50m, OpenPrice = 408.00m, HighPrice = 412.00m, LowPrice = 407.50m, Volume = 58000, RecordedAt = baseTime.AddDays(-1) },

                new CompanyPriceHistory { CompanyId = beximco.CompanyId, Price = 112.00m, OpenPrice = 110.00m, HighPrice = 113.50m, LowPrice = 109.50m, Volume = 85000, RecordedAt = baseTime.AddDays(-30) },
                new CompanyPriceHistory { CompanyId = beximco.CompanyId, Price = 115.00m, OpenPrice = 112.50m, HighPrice = 116.00m, LowPrice = 112.00m, Volume = 92000, RecordedAt = baseTime.AddDays(-20) },
                new CompanyPriceHistory { CompanyId = beximco.CompanyId, Price = 117.20m, OpenPrice = 115.00m, HighPrice = 118.00m, LowPrice = 114.50m, Volume = 78000, RecordedAt = baseTime.AddDays(-10) },
                new CompanyPriceHistory { CompanyId = beximco.CompanyId, Price = 118.40m, OpenPrice = 117.00m, HighPrice = 119.50m, LowPrice = 116.50m, Volume = 81000, RecordedAt = baseTime.AddDays(-1) },

                new CompanyPriceHistory { CompanyId = batbc.CompanyId, Price = 450.00m, OpenPrice = 448.00m, HighPrice = 455.00m, LowPrice = 445.00m, Volume = 31000, RecordedAt = baseTime.AddDays(-30) },
                new CompanyPriceHistory { CompanyId = batbc.CompanyId, Price = 458.00m, OpenPrice = 451.00m, HighPrice = 460.00m, LowPrice = 450.00m, Volume = 36000, RecordedAt = baseTime.AddDays(-15) },
                new CompanyPriceHistory { CompanyId = batbc.CompanyId, Price = 462.75m, OpenPrice = 458.50m, HighPrice = 465.00m, LowPrice = 457.00m, Volume = 42000, RecordedAt = baseTime.AddDays(-1) },

                new CompanyPriceHistory { CompanyId = square.CompanyId, Price = 218.00m, OpenPrice = 215.00m, HighPrice = 220.00m, LowPrice = 214.00m, Volume = 64000, RecordedAt = baseTime.AddDays(-30) },
                new CompanyPriceHistory { CompanyId = square.CompanyId, Price = 222.50m, OpenPrice = 218.50m, HighPrice = 224.00m, LowPrice = 217.50m, Volume = 71000, RecordedAt = baseTime.AddDays(-15) },
                new CompanyPriceHistory { CompanyId = square.CompanyId, Price = 226.80m, OpenPrice = 223.00m, HighPrice = 228.00m, LowPrice = 222.00m, Volume = 69000, RecordedAt = baseTime.AddDays(-1) }
            );
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Reference data seeding completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Reference seeding check encountered an issue (tables may not yet exist in unmigrated environment). Schema should be deployed via database/mysql/01_schema.sql.");
        }
    }
}
