using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.DTOs.Auth;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using ShareSync.Infrastructure.Security;
using ShareSync.Web.Controllers;
using Xunit;

namespace ShareSync.Tests;

public class AccountSettingsSecurityTests
{
    private readonly PasswordHasher _passwordHasher = new();

    private ShareSyncDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ShareSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ShareSyncDbContext(options);
    }

    private IConfiguration CreateTestConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:Secret", "ShareSyncSuperSecretKeyForAcademicProjectSecurity2026#LongEnoughKey" },
            { "Jwt:Issuer", "ShareSyncServer" },
            { "Jwt:Audience", "ShareSyncClient" },
            { "Jwt:ExpiryDays", "7" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(int? userId = null, string role = "INVESTOR", string? email = null)
        {
            UserId = userId;
            Role = role;
            Email = email;
            IsAuthenticated = userId.HasValue;
        }

        public int? UserId { get; }
        public string? Email { get; }
        public string? Role { get; }
        public bool IsAuthenticated { get; }
    }

    private (AuthService authService, ShareSyncDbContext context) SetupAuthService()
    {
        var context = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var tokenGenerator = new JwtTokenGenerator(config);
        var authService = new AuthService(context, _passwordHasher, tokenGenerator);
        return (authService, context);
    }

    [Fact]
    public async Task Test1_ValidPasswordChange_SuccessfullyUpdatesHash()
    {
        // 1. Valid password change
        var (authService, context) = SetupAuthService();
        var user = new AppUser
        {
            UserId = 101,
            Name = "Security User 1",
            Email = "secuser1@example.com",
            PasswordHash = _passwordHasher.HashPassword("InitialPassword123#"),
            Role = "INVESTOR"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "InitialPassword123#",
            NewPassword = "BrandNewPassword789!",
            ConfirmNewPassword = "BrandNewPassword789!"
        };

        var response = await authService.ChangePasswordAsync(101, request);

        Assert.True(response.Success);
        var updatedUser = await context.Users.FindAsync(101);
        Assert.NotNull(updatedUser);
        Assert.True(_passwordHasher.VerifyPassword("BrandNewPassword789!", updatedUser.PasswordHash));
        Assert.False(_passwordHasher.VerifyPassword("InitialPassword123#", updatedUser.PasswordHash));
    }

    [Fact]
    public async Task Test2_WrongCurrentPassword_ThrowsAppException()
    {
        // 2. Wrong current password
        var (authService, context) = SetupAuthService();
        var user = new AppUser
        {
            UserId = 102,
            Name = "Security User 2",
            Email = "secuser2@example.com",
            PasswordHash = _passwordHasher.HashPassword("ActualSecret123#"),
            Role = "INVESTOR"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "IncorrectPassword999!",
            NewPassword = "NewValidPassword456!",
            ConfirmNewPassword = "NewValidPassword456!"
        };

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            authService.ChangePasswordAsync(102, request));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("Current password is incorrect", ex.Message);
    }

    [Fact]
    public async Task Test3_PasswordConfirmationMismatch_ThrowsAppException()
    {
        // 3. Password confirmation mismatch
        var (authService, context) = SetupAuthService();
        var user = new AppUser
        {
            UserId = 103,
            Name = "Security User 3",
            Email = "secuser3@example.com",
            PasswordHash = _passwordHasher.HashPassword("CorrectPassword123#"),
            Role = "INVESTOR"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "CorrectPassword123#",
            NewPassword = "ValidNewPassword456!",
            ConfirmNewPassword = "MismatchNewPassword999!"
        };

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            authService.ChangePasswordAsync(103, request));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("do not match", ex.Message);
    }

    [Fact]
    public async Task Test4_InvalidPassword_TooShort_ThrowsAppException()
    {
        // 4. Invalid password (< 6 chars)
        var (authService, context) = SetupAuthService();
        var user = new AppUser
        {
            UserId = 104,
            Name = "Security User 4",
            Email = "secuser4@example.com",
            PasswordHash = _passwordHasher.HashPassword("ExistingSecret123#"),
            Role = "INVESTOR"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "ExistingSecret123#",
            NewPassword = "123",
            ConfirmNewPassword = "123"
        };

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            authService.ChangePasswordAsync(104, request));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("at least 6 characters", ex.Message);
    }

    [Fact]
    public async Task Test5_OldPasswordNoLongerWorks_AfterPasswordChange()
    {
        // 5. Old password no longer works
        var (authService, context) = SetupAuthService();
        var user = new AppUser
        {
            UserId = 105,
            Name = "Security User 5",
            Email = "secuser5@example.com",
            PasswordHash = _passwordHasher.HashPassword("OriginalSecret123#"),
            Role = "INVESTOR"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Change password
        await authService.ChangePasswordAsync(105, new ChangePasswordRequestDto
        {
            CurrentPassword = "OriginalSecret123#",
            NewPassword = "UpdatedSecret456#",
            ConfirmNewPassword = "UpdatedSecret456#"
        });

        // Attempt login with old password
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            authService.LoginAsync(new LoginRequestDto
            {
                Email = "secuser5@example.com",
                Password = "OriginalSecret123#"
            }));
    }

    [Fact]
    public async Task Test6_NewPasswordWorks_AfterPasswordChange()
    {
        // 6. New password works
        var (authService, context) = SetupAuthService();
        var user = new AppUser
        {
            UserId = 106,
            Name = "Security User 6",
            Email = "secuser6@example.com",
            PasswordHash = _passwordHasher.HashPassword("InitialSecret123#"),
            Role = "INVESTOR"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Change password
        await authService.ChangePasswordAsync(106, new ChangePasswordRequestDto
        {
            CurrentPassword = "InitialSecret123#",
            NewPassword = "UpdatedSecret456#",
            ConfirmNewPassword = "UpdatedSecret456#"
        });

        // Act: Login with new password
        var loginResponse = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "secuser6@example.com",
            Password = "UpdatedSecret456#"
        });

        // Assert
        Assert.True(loginResponse.Success);
        Assert.NotNull(loginResponse.Data);
        Assert.False(string.IsNullOrWhiteSpace(loginResponse.Data.Token));
        Assert.Equal(106, loginResponse.Data.UserId);
        Assert.Equal("secuser6@example.com", loginResponse.Data.Email);
    }

    [Fact]
    public async Task Test7_UnauthorizedAccess_ThrowsAppropriateException()
    {
        // 7. Unauthorized access
        var (authService, _) = SetupAuthService();

        // Case A: Unauthenticated user via AuthController
        var unauthenticatedUserService = new FakeCurrentUserService(userId: null);
        var controller = new AuthController(authService, unauthenticatedUserService);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            controller.ChangePassword(new ChangePasswordRequestDto
            {
                CurrentPassword = "SomePassword123#",
                NewPassword = "NewValidPassword456#"
            }, CancellationToken.None));

        // Case B: Non-existent user directly in service
        await Assert.ThrowsAsync<NotFoundException>(() =>
            authService.ChangePasswordAsync(999999, new ChangePasswordRequestDto
            {
                CurrentPassword = "SomePassword123#",
                NewPassword = "NewValidPassword456#"
            }));
    }

    [Fact]
    public async Task Test8_ExistingLogin_StillWorks_ForOtherUsers()
    {
        // 8. Existing login still works
        var (authService, context) = SetupAuthService();

        // User A changes password
        var userA = new AppUser
        {
            UserId = 1081,
            Name = "User A",
            Email = "userA@example.com",
            PasswordHash = _passwordHasher.HashPassword("UserAPassword123#"),
            Role = "INVESTOR"
        };

        // User B has untouched credentials
        var userB = new AppUser
        {
            UserId = 1082,
            Name = "User B",
            Email = "userB@example.com",
            PasswordHash = _passwordHasher.HashPassword("UserBPassword456#"),
            Role = "INVESTOR"
        };

        context.Users.AddRange(userA, userB);
        await context.SaveChangesAsync();

        // User A changes password
        await authService.ChangePasswordAsync(1081, new ChangePasswordRequestDto
        {
            CurrentPassword = "UserAPassword123#",
            NewPassword = "UserANewPassword789#",
            ConfirmNewPassword = "UserANewPassword789#"
        });

        // Act & Assert: User B's login is completely unaffected
        var loginB = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "userB@example.com",
            Password = "UserBPassword456#"
        });

        Assert.True(loginB.Success);
        Assert.NotNull(loginB.Data);
        Assert.Equal(1082, loginB.Data.UserId);
        Assert.False(string.IsNullOrWhiteSpace(loginB.Data.Token));
    }
}
