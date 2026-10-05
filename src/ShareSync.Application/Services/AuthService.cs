using Microsoft.EntityFrameworkCore;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Auth;
using ShareSync.Application.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Application.Services;

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
