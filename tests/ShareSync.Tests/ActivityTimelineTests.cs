using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Activities;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using Xunit;

namespace ShareSync.Tests;

public class ActivityTimelineTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new ShareSyncDbContext(options);

        // Seed users
        var user1 = new AppUser { UserId = 1, Name = "User One", Email = "user1@sharesync.com", PasswordHash = "hash" };
        var user2 = new AppUser { UserId = 2, Name = "User Two", Email = "user2@sharesync.com", PasswordHash = "hash" };
        context.Users.AddRange(user1, user2);

        // Seed portfolios
        var p1 = new Portfolio
        {
            PortfolioId = 1,
            UserId = 1,
            PortfolioName = "Long Term Portfolio",
            Description = "Tech and growth investments",
            CreatedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)
        };
        var pOther = new Portfolio
        {
            PortfolioId = 2,
            UserId = 2,
            PortfolioName = "User 2 Portfolio",
            CreatedAt = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc)
        };
        context.Portfolios.AddRange(p1, pOther);

        // Seed companies
        var sector = new Sector { SectorId = 1, SectorName = "Pharmaceuticals" };
        context.Sectors.Add(sector);

        var c1 = new Company { CompanyId = 1, CompanyName = "Beximco Pharma", TickerSymbol = "BEXIMCO", SectorId = 1, CurrentPrice = 120m };
        var c2 = new Company { CompanyId = 2, CompanyName = "Square Pharma", TickerSymbol = "SQURPHARMA", SectorId = 1, CurrentPrice = 240m };
        context.Companies.AddRange(c1, c2);

        // Seed transactions & audits for User 1
        var t1 = new Transaction
        {
            TransactionId = 101,
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "BUY",
            Quantity = 100,
            PricePerShare = 115.00m,
            TransactionDate = new DateTime(2026, 2, 10, 14, 30, 0, DateTimeKind.Utc),
            Portfolio = p1,
            Company = c1
        };

        var audit1 = new TransactionAudit
        {
            AuditId = 501,
            TransactionId = 101,
            ActionType = "INSERT",
            ActionDate = new DateTime(2026, 2, 10, 14, 30, 0, DateTimeKind.Utc),
            ChangedBy = "User #1",
            Details = "BUY 100 shares of BEXIMCO @ ৳115.00",
            Transaction = t1
        };

        var t2 = new Transaction
        {
            TransactionId = 102,
            PortfolioId = 1,
            CompanyId = 1,
            TransactionType = "SELL",
            Quantity = 20,
            PricePerShare = 130.00m,
            TransactionDate = new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc),
            Portfolio = p1,
            Company = c1
        };

        var audit2 = new TransactionAudit
        {
            AuditId = 502,
            TransactionId = 102,
            ActionType = "INSERT",
            ActionDate = new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc),
            ChangedBy = "User #1",
            Details = "SELL 20 shares of BEXIMCO @ ৳130.00",
            Transaction = t2
        };

        context.Transactions.AddRange(t1, t2);
        context.TransactionAudits.AddRange(audit1, audit2);

        // Seed transaction for User 2 (must remain isolated)
        var tOther = new Transaction
        {
            TransactionId = 201,
            PortfolioId = 2,
            CompanyId = 2,
            TransactionType = "BUY",
            Quantity = 500,
            PricePerShare = 230.00m,
            TransactionDate = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc),
            Portfolio = pOther,
            Company = c2
        };
        var auditOther = new TransactionAudit
        {
            AuditId = 601,
            TransactionId = 201,
            ActionType = "INSERT",
            ActionDate = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc),
            ChangedBy = "User #2",
            Details = "BUY 500 shares of SQURPHARMA @ ৳230.00",
            Transaction = tOther
        };
        context.Transactions.Add(tOther);
        context.TransactionAudits.Add(auditOther);

        // Seed watchlist for User 1
        var w1 = new Watchlist
        {
            WatchlistId = 1,
            UserId = 1,
            WatchlistName = "Main Watchlist",
            CreatedAt = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc)
        };
        var wItem1 = new WatchlistItem
        {
            WatchlistId = 1,
            CompanyId = 2,
            TargetPrice = 250.00m,
            AddedAt = new DateTime(2026, 2, 1, 16, 0, 0, DateTimeKind.Utc),
            Watchlist = w1,
            Company = c2
        };
        context.Watchlists.Add(w1);
        context.WatchlistItems.Add(wItem1);

        // Seed goal for User 1
        var goal1 = new PortfolioGoal
        {
            GoalId = 1,
            UserId = 1,
            PortfolioId = 1,
            GoalType = "TARGET_PORTFOLIO_VALUE",
            TargetValue = 500000m,
            Title = "Reach 500K",
            CreatedAt = new DateTime(2026, 1, 20, 8, 0, 0, DateTimeKind.Utc),
            Portfolio = p1
        };
        context.PortfolioGoals.Add(goal1);

        // Seed alert for User 1
        var alert1 = new Alert
        {
            AlertId = 1,
            UserId = 1,
            CompanyId = 1,
            AlertType = "PRICE_ABOVE",
            ThresholdValue = 125.00m,
            CreatedAt = new DateTime(2026, 1, 25, 10, 0, 0, DateTimeKind.Utc),
            TriggeredAt = new DateTime(2026, 2, 28, 15, 0, 0, DateTimeKind.Utc),
            Message = "BEXIMCO reached ৳125.00",
            Company = c1
        };
        context.Alerts.Add(alert1);

        // Seed dividend for company 1
        var div1 = new Dividend
        {
            DividendId = 1,
            CompanyId = 1,
            DividendPerShare = 5.00m,
            DeclarationDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            PaymentDate = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
            Company = c1
        };
        context.Dividends.Add(div1);

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task EventsAppearCorrectly_AllCategoriesRepresented()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ActivityTimelineService(context);

        var filter = new UserActivityFilterDto { Page = 1, PageSize = 50 };
        var res = await service.GetUserTimelineAsync(userId: 1, filter);

        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.NotEmpty(res.Data.Items);

        var eventTypes = res.Data.Items.Select(i => i.EventType).ToHashSet();

        // Check key event types
        Assert.Contains("BUY", eventTypes);
        Assert.Contains("SELL", eventTypes);
        Assert.Contains("PORTFOLIO_CREATED", eventTypes);
        Assert.Contains("WATCHLIST_ADDED", eventTypes);
        Assert.Contains("GOAL_CREATED", eventTypes);
        Assert.Contains("ALERT_CREATED", eventTypes);
        Assert.Contains("ALERT_TRIGGERED", eventTypes);
        Assert.Contains("DIVIDEND_RECORDED", eventTypes);
    }

    [Fact]
    public async Task OrderingByTimestamp_DescendingOrderEnforced()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ActivityTimelineService(context);

        var filter = new UserActivityFilterDto { Page = 1, PageSize = 50 };
        var res = await service.GetUserTimelineAsync(userId: 1, filter);

        Assert.True(res.Success);
        var items = res.Data!.Items;
        Assert.True(items.Count > 1);

        for (int i = 0; i < items.Count - 1; i++)
        {
            Assert.True(items[i].Timestamp >= items[i + 1].Timestamp,
                $"Item at {i} ({items[i].Timestamp}) should be >= Item at {i+1} ({items[i + 1].Timestamp})");
        }
    }

    [Fact]
    public async Task Pagination_WorksAccurately()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ActivityTimelineService(context);

        var p1Filter = new UserActivityFilterDto { Page = 1, PageSize = 3 };
        var p1Res = await service.GetUserTimelineAsync(userId: 1, p1Filter);

        Assert.True(p1Res.Success);
        Assert.Equal(1, p1Res.Data!.Page);
        Assert.Equal(3, p1Res.Data.PageSize);
        Assert.Equal(3, p1Res.Data.Items.Count);
        Assert.True(p1Res.Data.TotalItems > 3);
        Assert.True(p1Res.Data.TotalPages >= 2);

        var p2Filter = new UserActivityFilterDto { Page = 2, PageSize = 3 };
        var p2Res = await service.GetUserTimelineAsync(userId: 1, p2Filter);

        Assert.Equal(2, p2Res.Data!.Page);
        Assert.Equal(3, p2Res.Data.Items.Count);

        // Verify page 1 and page 2 don't overlap
        var p1Ids = p1Res.Data.Items.Select(x => x.ActivityId).ToHashSet();
        Assert.All(p2Res.Data.Items, item => Assert.DoesNotContain(item.ActivityId, p1Ids));
    }

    [Fact]
    public async Task UserIsolation_NeverExposesAnotherUsersActivity()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ActivityTimelineService(context);

        var res = await service.GetUserTimelineAsync(userId: 1, new UserActivityFilterDto { PageSize = 100 });

        Assert.True(res.Success);
        // User 2's transaction 201 or audit 601 must NOT appear
        Assert.DoesNotContain(res.Data!.Items, i => i.RelatedEntityId == 201);
        Assert.DoesNotContain(res.Data.Items, i => i.ActivityId == "audit_601");
        Assert.DoesNotContain(res.Data.Items, i => i.Title.Contains("User 2 Portfolio"));
    }

    [Fact]
    public async Task NoDuplicateActivity_TransactionAuditsMappedSingleTime()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ActivityTimelineService(context);

        var res = await service.GetUserTimelineAsync(userId: 1, new UserActivityFilterDto { PageSize = 100 });

        Assert.True(res.Success);
        var tx101Entries = res.Data!.Items.Where(i => i.RelatedEntityType == "Transaction" && i.RelatedEntityId == 101).ToList();

        // Exactly 1 entry for transaction 101 (from audit)
        Assert.Single(tx101Entries);
    }

    [Fact]
    public async Task ExistingAuditRecordsRemainIntact_NoMutationsOccur()
    {
        using var context = CreateInMemoryDbContext();
        var initialAuditCount = await context.TransactionAudits.CountAsync();
        var initialAudits = await context.TransactionAudits.AsNoTracking().ToListAsync();

        var service = new ActivityTimelineService(context);
        var res = await service.GetUserTimelineAsync(userId: 1, new UserActivityFilterDto());

        Assert.True(res.Success);

        // Ensure database state was not modified
        var postAuditCount = await context.TransactionAudits.CountAsync();
        Assert.Equal(initialAuditCount, postAuditCount);

        var postAudits = await context.TransactionAudits.AsNoTracking().ToListAsync();
        for (int i = 0; i < initialAudits.Count; i++)
        {
            Assert.Equal(initialAudits[i].AuditId, postAudits[i].AuditId);
            Assert.Equal(initialAudits[i].Details, postAudits[i].Details);
            Assert.Equal(initialAudits[i].ActionType, postAudits[i].ActionType);
        }
    }

    [Fact]
    public async Task FilterByEventType_FiltersSpecifically()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ActivityTimelineService(context);

        var filter = new UserActivityFilterDto { EventType = "TRANSACTION" };
        var res = await service.GetUserTimelineAsync(userId: 1, filter);

        Assert.True(res.Success);
        Assert.NotEmpty(res.Data!.Items);
        Assert.All(res.Data.Items, i =>
        {
            Assert.True(i.EventType is "BUY" or "SELL" or "TRANSACTION_UPDATED" or "TRANSACTION_DELETED");
        });
    }

    [Fact]
    public async Task InvalidDateRange_ThrowsBadRequestAppException()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ActivityTimelineService(context);

        var filter = new UserActivityFilterDto
        {
            StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.GetUserTimelineAsync(userId: 1, filter));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("Start date cannot be after end date", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
