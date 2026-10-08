using System.Security.Cryptography;
using ShareSync.Application.Common.Interfaces;

namespace ShareSync.Infrastructure.Security;

/// <summary>
/// SECURITY: Password Hashing
/// SECURITY: PBKDF2 SHA-256 Cryptographic Standard
/// 
/// Cryptographic Specifications:
/// - Salt: 16 bytes (128-bit) generated via cryptographically secure RandomNumberGenerator.
/// - Iterations: 100,000 work factor cycles (OWASP recommended standard for PBKDF2-HMAC-SHA256).
/// - Output Key: 32 bytes (256-bit) derived subkey.
/// - Storage Format: Base64(Salt) + "." + Base64(Hash).
/// 
/// SECURITY: Timing Attack Protection
/// Verification utilizes CryptographicOperations.FixedTimeEquals to prevent side-channel timing attacks
/// where an attacker measures string comparison duration to infer hash characters.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128 bit
    private const int KeySize = 32;  // 256 bit
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>
    /// SECURITY: Password Hashing
    /// Generates a unique 128-bit salt and derives a 256-bit PBKDF2 hash using HMAC-SHA256 across 100,000 rounds.
    /// </summary>
    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// SECURITY: Password Verification
    /// SECURITY: Timing Attack Protection
    /// Re-derives PBKDF2 hash using the stored salt and evaluates with constant-time equality check.
    /// </summary>
    public bool VerifyPassword(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);

            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}
