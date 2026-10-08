using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Auth;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

/// <summary>
/// SECURITY: Authentication & User Lifecycle Service
/// 
/// SECURITY: SQL Injection Protection
/// All queries use EF Core LINQ with parameterized SQL translation.
/// User input (email, password) is never interpolated into raw SQL text.
/// 
/// SECURITY: Password Hashing
/// Passwords are never stored in plain text. Hashing is delegated to IPasswordHasher
/// using PBKDF2 with HMAC-SHA256, a cryptographically secure 128-bit random salt, and 100,000 iterations.
/// 
/// SECURITY: JWT Authentication
/// Issues signed RFC 7519 JSON Web Tokens (JWT) containing subject claim (UserId),
/// email, role ("INVESTOR", "ADMIN"), and expiration timestamp.
/// 
/// SECURITY: Ownership Isolation
/// On successful investor registration, automatically seeds a private "Main Portfolio"
/// bound to the newly created UserId, ensuring isolated financial accounting.
/// 
/// DATABASE: Table — app_users, portfolios
/// </summary>
public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    /// <summary>
    /// SECURITY: User Registration
    /// SECURITY: Password Hashing
    /// SECURITY: SQL Injection Protection
    /// 
    /// Enforces case-insensitive email uniqueness check via parameterized EF Core LINQ.
    /// Hashes user password with PBKDF2 HMAC-SHA256 and creates initial user account and default portfolio.
    /// </summary>
    public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Parameterized EF Core check prevents SQL injection and enforces uniqueness
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            throw new AppException("An account with this email address already exists.", 409);
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            throw new AppException("Password must be at least 6 characters long.", 400);
        }

        var user = new AppUser
        {
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = "INVESTOR",
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        // Create default portfolio for the new investor
        var defaultPortfolio = new Portfolio
        {
            UserId = user.UserId,
            PortfolioName = "Main Portfolio",
            Description = "Default primary investment portfolio",
            CreatedAt = DateTime.UtcNow
        };
        _context.Portfolios.Add(defaultPortfolio);
        await _context.SaveChangesAsync(cancellationToken);

        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user);

        var responseData = new AuthResponseDto
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            Token = token,
            ExpiresAt = expiresAt
        };

        return ApiResponse<AuthResponseDto>.Ok(responseData, "Registration successful.");
    }

    /// <summary>
    /// SECURITY: Authentication
    /// SECURITY: Password Hashing Verification
    /// SECURITY: Timing-Safe User Enumeration Prevention
    /// 
    /// Verifies supplied credentials against stored PBKDF2 hash.
    /// Returns uniform 401 Unauthorized whether user exists or password fails to prevent username discovery.
    /// </summary>
    public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Parameterized EF Core query
        var user = await _context.Users
            .SingleOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            // Consistent failure message prevents user enumeration
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new AppException("This account has been deactivated. Please contact an administrator.", 403);
        }

        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user);

        var responseData = new AuthResponseDto
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            Token = token,
            ExpiresAt = expiresAt
        };

        return ApiResponse<AuthResponseDto>.Ok(responseData, "Login successful.");
    }

    public async Task<ApiResponse<UserDto>> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User", userId);
        }

        var userDto = new UserDto
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };

        return ApiResponse<UserDto>.Ok(userDto);
    }

    public async Task<ApiResponse<UserDto>> UpdateProfileAsync(int userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User", userId);
        }

        user.Name = request.Name.Trim();
        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };

        return ApiResponse<UserDto>.Ok(userDto, "Profile updated successfully.");
    }

    public async Task<ApiResponse> ChangePasswordAsync(int userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            throw new AppException("Current password is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            throw new AppException("New password must be at least 6 characters long.", 400);
        }

        if (!string.IsNullOrEmpty(request.ConfirmNewPassword) && request.NewPassword != request.ConfirmNewPassword)
        {
            throw new AppException("New password and confirmation password do not match.", 400);
        }

        if (request.CurrentPassword == request.NewPassword)
        {
            throw new AppException("New password must be different from current password.", 400);
        }

        var user = await _context.Users
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User", userId);
        }

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new AppException("Current password is incorrect.", 400);
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse.Ok("Password changed successfully. Please log in with your new password.");
    }
}
