using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Notifications;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using ShareSync.Web.Controllers;
using Xunit;

namespace ShareSync.Tests;

public class NotificationTests
{
    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ShareSyncDbContext(options);
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(int? userId = 1, string role = "INVESTOR")
        {
            UserId = userId;
            Role = role;
            Email = "user@sharesync.com";
            IsAuthenticated = userId.HasValue;
        }

        public int? UserId { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public bool IsAuthenticated { get; set; }
    }

    // =========================================================================
    // 1. NOTIFICATION CREATION & VALIDATION
    // =========================================================================

    [Fact]
    public async Task CreateNotification_WithValidData_CreatesAndReturnsNotification()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

        var request = new CreateNotificationDto
        {
            UserId = 1,
            NotificationType = "PRICE_ALERT",
            Title = "GP Target Reached",
            Message = "Grameenphone crossed your target price of ৳350.00.",
            RelatedEntityType = "COMPANY",
            RelatedEntityId = 10
        };

        // Act
        var result = await service.CreateNotificationAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.UserId);
        Assert.Equal("PRICE_ALERT", result.Data.NotificationType);
        Assert.Equal("GP Target Reached", result.Data.Title);
        Assert.False(result.Data.IsRead);

        var inDb = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == 1);
        Assert.NotNull(inDb);
        Assert.Equal("GP Target Reached", inDb.Title);
    }

    [Fact]
    public async Task CreateNotification_WithDuplicateData_SuppressesDuplicate()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

        var request = new CreateNotificationDto
        {
            UserId = 1,
            NotificationType = "PRICE_ALERT",
            Title = "Duplicate Target",
            Message = "Price crossed threshold.",
            RelatedEntityType = "ALERT",
            RelatedEntityId = 99
        };

        // Act
        var res1 = await service.CreateNotificationAsync(request);
        var res2 = await service.CreateNotificationAsync(request);

        // Assert
        Assert.True(res1.Success);
        Assert.True(res2.Success);
        Assert.Contains("duplicate suppressed", res2.Message, StringComparison.OrdinalIgnoreCase);

        // Only 1 record should exist in the database
        var count = await context.Notifications.CountAsync(n => n.UserId == 1 && n.Title == "Duplicate Target");
        Assert.Equal(1, count);
    }

    // =========================================================================
    // 2. USER OWNERSHIP & LISTING
    // =========================================================================

    [Fact]
    public async Task GetUserNotifications_EnforcesUserOwnership()
    {
        // Arrange: User 1 and User 2 have separate notifications
        using var context = CreateInMemoryDbContext();
        var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

        context.Notifications.AddRange(
            new Notification { NotificationId = 1, UserId = 1, Title = "User 1 Notif A", Message = "Msg", NotificationType = "SYSTEM", IsRead = false },
            new Notification { NotificationId = 2, UserId = 1, Title = "User 1 Notif B", Message = "Msg", NotificationType = "SYSTEM", IsRead = true },
            new Notification { NotificationId = 3, UserId = 2, Title = "User 2 Private Notif", Message = "Private", NotificationType = "SYSTEM", IsRead = false }
        );
        await context.SaveChangesAsync();

        // Act: User 1 fetches notifications
        var user1Result = await service.GetUserNotificationsAsync(userId: 1);
        var user2Result = await service.GetUserNotificationsAsync(userId: 2);

        // Assert: User 1 only sees their 2 notifications; User 2 only sees their 1 notification
        Assert.True(user1Result.Success);
        Assert.Equal(2, user1Result.Data!.Count);
        Assert.DoesNotContain(user1Result.Data, n => n.UserId == 2);

        Assert.True(user2Result.Success);
        Assert.Single(user2Result.Data!);
        Assert.Equal("User 2 Private Notif", user2Result.Data![0].Title);
    }

    // =========================================================================
    // 3. UNREAD COUNT & SUMMARY
    // =========================================================================

    [Fact]
    public async Task GetUnreadCount_AndSummary_ReturnsAccurateMetrics()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

        context.Notifications.AddRange(
            new Notification { NotificationId = 1, UserId = 5, Title = "N1", Message = "M1", NotificationType = "SYSTEM", IsRead = false },
            new Notification { NotificationId = 2, UserId = 5, Title = "N2", Message = "M2", NotificationType = "SYSTEM", IsRead = false },
            new Notification { NotificationId = 3, UserId = 5, Title = "N3", Message = "M3", NotificationType = "SYSTEM", IsRead = true }
        );
        await context.SaveChangesAsync();

        // Act
        var countResult = await service.GetUnreadCountAsync(userId: 5);
        var summaryResult = await service.GetNotificationSummaryAsync(userId: 5);

        // Assert
        Assert.True(countResult.Success);
        Assert.Equal(2, countResult.Data);

        Assert.True(summaryResult.Success);
        Assert.Equal(2, summaryResult.Data!.UnreadCount);
        Assert.Equal(3, summaryResult.Data.TotalCount);
        Assert.Equal(3, summaryResult.Data.Notifications.Count);
    }

    // =========================================================================
    // 4. MARK AS READ & MARK ALL READ
    // =========================================================================

    [Fact]
    public async Task MarkAsRead_WithValidNotification_UpdatesIsReadToTrue()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

        var notif = new Notification
        {
            NotificationId = 10,
            UserId = 1,
            Title = "Unread Alert",
            Message = "Alert message",
            NotificationType = "PRICE_ALERT",
            IsRead = false
        };
        context.Notifications.Add(notif);
        await context.SaveChangesAsync();

        // Act
        var result = await service.MarkAsReadAsync(notificationId: 10, userId: 1);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Data!.IsRead);

        var inDb = await context.Notifications.FindAsync(10);
        Assert.True(inDb!.IsRead);
    }

    [Fact]
    public async Task MarkAsRead_ByDifferentUser_Throws403Forbidden()
    {
        // Arrange: Guardrail - Cross-user modification forbidden
        using var context = CreateInMemoryDbContext();
        var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

        var notif = new Notification
        {
            NotificationId = 20,
            UserId = 1,
            Title = "User 1 Notif",
            Message = "Msg",
            NotificationType = "SYSTEM",
            IsRead = false
        };
        context.Notifications.Add(notif);
        await context.SaveChangesAsync();

        // Act & Assert: User 2 attempting to modify User 1's notification throws 403
        var ex = await Assert.ThrowsAsync<AppException>(() => service.MarkAsReadAsync(notificationId: 20, userId: 2));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task MarkAllAsRead_MarksAllUserUnreadNotificationsAsRead()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

        context.Notifications.AddRange(
            new Notification { NotificationId = 31, UserId = 8, Title = "A", Message = "M", NotificationType = "SYSTEM", IsRead = false },
            new Notification { NotificationId = 32, UserId = 8, Title = "B", Message = "M", NotificationType = "SYSTEM", IsRead = false },
            new Notification { NotificationId = 33, UserId = 9, Title = "C", Message = "M", NotificationType = "SYSTEM", IsRead = false } // User 9
        );
        await context.SaveChangesAsync();

        // Act: User 8 marks all read
        var result = await service.MarkAllAsReadAsync(userId: 8);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Data);

        var user8Unread = await context.Notifications.CountAsync(n => n.UserId == 8 && !n.IsRead);
        Assert.Equal(0, user8Unread);

        // User 9's unread notification must remain untouched
        var user9Unread = await context.Notifications.CountAsync(n => n.UserId == 9 && !n.IsRead);
        Assert.Equal(1, user9Unread);
    }

    // =========================================================================
    // 5. DELETION & OWNERSHIP
    // =========================================================================

    [Fact]
    public async Task DeleteNotification_EnforcesOwnership()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new NotificationService(context, NullLogger<NotificationService>.Instance);

        context.Notifications.Add(new Notification
        {
            NotificationId = 40,
            UserId = 1,
            Title = "To Delete",
            Message = "M",
            NotificationType = "SYSTEM"
        });
        await context.SaveChangesAsync();

        // Act & Assert 1: Non-owner gets 403 Forbidden
        var ex = await Assert.ThrowsAsync<AppException>(() => service.DeleteNotificationAsync(40, userId: 99));
        Assert.Equal(403, ex.StatusCode);

        // Act & Assert 2: Owner deletes successfully
        var delRes = await service.DeleteNotificationAsync(40, userId: 1);
        Assert.True(delRes.Success);
        var deleted = await context.Notifications.FindAsync(40);
        Assert.Null(deleted);
    }

    // =========================================================================
    // 6. ALERT -> NOTIFICATION INTEGRATION & DUPLICATE PREVENTION
    // =========================================================================

    [Fact]
    public async Task AlertToNotificationIntegration_WhenAlertTriggers_GeneratesNotification()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var notifService = new NotificationService(context, NullLogger<NotificationService>.Instance);
        var alertService = new AlertService(context, NullLogger<AlertService>.Instance, notifService);

        var company = new Company
        {
            CompanyId = 1,
            TickerSymbol = "GP",
            CompanyName = "Grameenphone",
            CurrentPrice = 410.00m,
            IsActive = true
        };
        context.Companies.Add(company);

        var alert = new Alert
        {
            AlertId = 1,
            UserId = 10,
            CompanyId = 1,
            AlertType = "PRICE_ABOVE",
            ThresholdValue = 400.00m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Alerts.Add(alert);
        await context.SaveChangesAsync();

        // Act: Evaluate price alerts
        var triggeredCount = await alertService.EvaluatePriceAlertsAsync();

        // Assert: Alert triggered
        Assert.Equal(1, triggeredCount);

        // Assert: Notification automatically generated in Notification Center
        var notifs = await notifService.GetUserNotificationsAsync(userId: 10);
        Assert.True(notifs.Success);
        Assert.Single(notifs.Data!);

        var notif = notifs.Data![0];
        Assert.Equal("PRICE_ALERT", notif.NotificationType);
        Assert.Equal("Price Alert: GP", notif.Title);
        Assert.Contains("410.00", notif.Message);
        Assert.Equal("ALERT", notif.RelatedEntityType);
        Assert.Equal(1, notif.RelatedEntityId);
    }

    [Fact]
    public async Task AlertToNotificationIntegration_DuplicatePrevention_SkipsDuplicateNotification()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var notifService = new NotificationService(context, NullLogger<NotificationService>.Instance);

        var alert = new Alert
        {
            AlertId = 5,
            UserId = 20,
            AlertType = "PRICE_ABOVE",
            ThresholdValue = 300m,
            IsActive = false,
            TriggeredAt = DateTime.UtcNow,
            Message = "Threshold reached."
        };

        // Act: Generate first time
        var n1 = await notifService.CreateNotificationFromAlertAsync(alert, "Price Alert: BATS", "Threshold reached.");
        // Act: Attempt to generate second time for the same event
        var n2 = await notifService.CreateNotificationFromAlertAsync(alert, "Price Alert: BATS", "Threshold reached.");

        // Assert
        Assert.NotNull(n1);
        Assert.Null(n2); // Second attempt suppressed by duplicate prevention

        var totalNotifs = await context.Notifications.CountAsync(n => n.UserId == 20 && n.RelatedEntityId == 5);
        Assert.Equal(1, totalNotifs);
    }

    // =========================================================================
    // 7. CONTROLLER SECURITY & AUTHORIZATION
    // =========================================================================

    [Fact]
    public void NotificationsController_HasAuthorizeAttribute()
    {
        var attr = typeof(NotificationsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .FirstOrDefault();

        Assert.NotNull(attr);
    }

    [Fact]
    public async Task NotificationsController_UnauthenticatedAccess_Throws401()
    {
        using var context = CreateInMemoryDbContext();
        var service = new NotificationService(context, NullLogger<NotificationService>.Instance);
        var unauthenticatedUserService = new FakeCurrentUserService(userId: null);
        var controller = new NotificationsController(service, unauthenticatedUserService);

        var ex = await Assert.ThrowsAsync<AppException>(() => controller.GetUserNotifications(null));
        Assert.Equal(401, ex.StatusCode);
    }
}
