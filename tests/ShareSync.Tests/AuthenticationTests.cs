using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.DTOs.Auth;
using ShareSync.Application.Services;
using ShareSync.Domain.Entities;
using ShareSync.Infrastructure.Data;
using ShareSync.Infrastructure.Security;
using Xunit;

namespace ShareSync.Tests;

public class AuthenticationTests
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
            {"Jwt:Secret", "ShareSyncSuperSecretKeyForAcademicProjectSecurity2026#LongEnoughKey"},
            {"Jwt:Issuer", "ShareSyncServer"},
            {"Jwt:Audience", "ShareSyncClient"},
            {"Jwt:ExpiryDays", "7"}
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task Register_WithValidCredentials_ReturnsSuccessAndJwtToken()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var tokenGenerator = new JwtTokenGenerator(config);
        var authService = new AuthService(context, _passwordHasher, tokenGenerator);

        var request = new RegisterRequestDto
        {
            Name = "John Investor",
            Email = "john.investor@example.com",
            Password = "Password123#"
        };

        // Act
        var response = await authService.RegisterAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("john.investor@example.com", response.Data.Email);
        Assert.Equal("John Investor", response.Data.Name);
        Assert.Equal("INVESTOR", response.Data.Role);
        Assert.False(string.IsNullOrWhiteSpace(response.Data.Token));

        // Verify database state: user was saved with hashed password and default portfolio was created
        var savedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "john.investor@example.com");
        Assert.NotNull(savedUser);
        Assert.NotEqual("Password123#", savedUser.PasswordHash);
        Assert.True(_passwordHasher.VerifyPassword("Password123#", savedUser.PasswordHash));

        var defaultPortfolio = await context.Portfolios.FirstOrDefaultAsync(p => p.UserId == savedUser.UserId);
        Assert.NotNull(defaultPortfolio);
        Assert.Equal("Main Portfolio", defaultPortfolio.PortfolioName);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ThrowsConflictException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var tokenGenerator = new JwtTokenGenerator(config);
        var authService = new AuthService(context, _passwordHasher, tokenGenerator);

        var request1 = new RegisterRequestDto
        {
            Name = "First User",
            Email = "duplicate@example.com",
            Password = "Password123#"
        };
        await authService.RegisterAsync(request1);

        var request2 = new RegisterRequestDto
        {
            Name = "Second User",
            Email = "duplicate@example.com",
            Password = "AnotherPassword123#"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppException>(() => authService.RegisterAsync(request2));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Register_WithCaseInsensitiveDuplicateEmail_ThrowsConflictException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var tokenGenerator = new JwtTokenGenerator(config);
        var authService = new AuthService(context, _passwordHasher, tokenGenerator);

        var request1 = new RegisterRequestDto
        {
            Name = "User One",
            Email = "investor@example.com",
            Password = "Password123#"
        };
        await authService.RegisterAsync(request1);

        var request2 = new RegisterRequestDto
        {
            Name = "User Two",
            Email = "INVESTOR@EXAMPLE.COM",
            Password = "Password123#"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppException>(() => authService.RegisterAsync(request2));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccessAndJwtToken()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var tokenGenerator = new JwtTokenGenerator(config);
        var authService = new AuthService(context, _passwordHasher, tokenGenerator);

        await authService.RegisterAsync(new RegisterRequestDto
        {
            Name = "Active Trader",
            Email = "trader@example.com",
            Password = "SecurePassword123!"
        });

        // Act
        var loginResponse = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "trader@example.com",
            Password = "SecurePassword123!"
        });

        // Assert
        Assert.True(loginResponse.Success);
        Assert.NotNull(loginResponse.Data);
        Assert.Equal("trader@example.com", loginResponse.Data.Email);
        Assert.False(string.IsNullOrWhiteSpace(loginResponse.Data.Token));
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ThrowsUnauthorizedException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var tokenGenerator = new JwtTokenGenerator(config);
        var authService = new AuthService(context, _passwordHasher, tokenGenerator);

        await authService.RegisterAsync(new RegisterRequestDto
        {
            Name = "Test User",
            Email = "test@example.com",
            Password = "CorrectPassword123#"
        });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => authService.LoginAsync(new LoginRequestDto
        {
            Email = "test@example.com",
            Password = "WrongPassword999!"
        }));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_ThrowsUnauthorizedException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var tokenGenerator = new JwtTokenGenerator(config);
        var authService = new AuthService(context, _passwordHasher, tokenGenerator);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => authService.LoginAsync(new LoginRequestDto
        {
            Email = "ghost@example.com",
            Password = "AnyPassword123#"
        }));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public void PasswordHasher_CreatesDifferentHashesForSamePassword_DueToUniqueSalt()
    {
        // Arrange
        const string password = "MyTestPassword123!";

        // Act
        var hash1 = _passwordHasher.HashPassword(password);
        var hash2 = _passwordHasher.HashPassword(password);

        // Assert
        Assert.NotEqual(hash1, hash2);
        Assert.True(_passwordHasher.VerifyPassword(password, hash1));
        Assert.True(_passwordHasher.VerifyPassword(password, hash2));
    }

    [Fact]
    public void PasswordHasher_RejectsIncorrectPassword()
    {
        // Arrange
        const string password = "CorrectPassword123!";
        var hash = _passwordHasher.HashPassword(password);

        // Act
        var result = _passwordHasher.VerifyPassword("IncorrectPassword123!", hash);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("' OR 1=1 --")]
    [InlineData("; DROP TABLE app_users; --")]
    public async Task SqlInjection_InLoginCredentials_IsHandledSafelyAndRejected(string maliciousInput)
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var config = CreateTestConfiguration();
        var tokenGenerator = new JwtTokenGenerator(config);
        var authService = new AuthService(context, _passwordHasher, tokenGenerator);

        // Act & Assert - Attempt SQL injection as Email
        await Assert.ThrowsAsync<UnauthorizedException>(() => authService.LoginAsync(new LoginRequestDto
        {
            Email = maliciousInput,
            Password = "SomePassword"
        }));

        // Attempt SQL injection as Password
        await Assert.ThrowsAsync<UnauthorizedException>(() => authService.LoginAsync(new LoginRequestDto
        {
            Email = "realuser@example.com",
            Password = maliciousInput
        }));
    }

    [Fact]
    public void JwtTokenGenerator_EmbedsExpectedClaims()
    {
        // Arrange
        var config = CreateTestConfiguration();
        var tokenGenerator = new JwtTokenGenerator(config);

        var user = new AppUser
        {
            UserId = 42,
            Name = "Token Tester",
            Email = "tokentester@example.com",
            Role = "INVESTOR"
        };

        // Act
        var (token, expiresAt) = tokenGenerator.GenerateToken(user);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(expiresAt > DateTime.UtcNow);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("42", jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid" || c.Type == "sub").Value);
        Assert.Equal("tokentester@example.com", jwt.Claims.First(c => c.Type == ClaimTypes.Email || c.Type == "email").Value);
        Assert.Equal("INVESTOR", jwt.Claims.First(c => c.Type == ClaimTypes.Role || c.Type == "role").Value);
    }
}
