using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Domain.Entities;

namespace ShareSync.Infrastructure.Security;

/// <summary>
/// SECURITY: JWT Token Generation
/// SECURITY: Authentication & Claims Principal Factory
/// 
/// Issues cryptographically signed JSON Web Tokens (HMAC-SHA256) complying with RFC 7519.
/// Claims embedded into token payload:
/// - ClaimTypes.NameIdentifier: UserId (string representation)
/// - ClaimTypes.Email: User normalized email
/// - ClaimTypes.Name: User display name
/// - ClaimTypes.Role: System authorization role ("INVESTOR" or "ADMIN")
/// - userId: Custom claim for direct client-side parsing
/// 
/// CONFIGURATION: JWT Settings
/// Reads signing key from configuration ("Jwt:Secret") without hardcoding secrets in code.
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// SECURITY: JWT Generation
    /// Creates signed bearer token with configured issuer, audience, and expiration period (default 7 days).
    /// </summary>
    public (string Token, DateTime ExpiresAt) GenerateToken(AppUser user)
    {
        var secret = _configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret configuration is missing.");
        var issuer = _configuration["Jwt:Issuer"] ?? "ShareSyncServer";
        var audience = _configuration["Jwt:Audience"] ?? "ShareSyncClient";
        var expiryDays = int.TryParse(_configuration["Jwt:ExpiryDays"], out var days) ? days : 7;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddDays(expiryDays);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role),
            new("userId", user.UserId.ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return (tokenHandler.WriteToken(token), expiresAt);
    }
}
