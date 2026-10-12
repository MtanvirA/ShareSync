namespace ShareSync.Application.Common.Interfaces;

public interface IMfaService
{
    /// <summary>
    /// Generates a cryptographically random Base32-encoded secret key for TOTP enrollment.
    /// </summary>
    string GenerateSecretKey(int byteLength = 20);

    /// <summary>
    /// Constructs a standard otpauth:// URI for QR code generation in authenticator apps.
    /// </summary>
    string GenerateTotpUri(string email, string secretKey, string issuer = "ShareSync");

    /// <summary>
    /// Validates a 6-digit TOTP code against the secret key using RFC 6238 HMAC-SHA1.
    /// Supports a configurable drift window (default ±1 timestep = 30 seconds).
    /// </summary>
    bool ValidateTotpCode(string secretKey, string code, int allowedDriftSteps = 1, DateTime? timestamp = null);

    /// <summary>
    /// Returns the current implementation status and technical capability of the MFA foundation.
    /// </summary>
    MfaFoundationStatus GetFoundationStatus();
}

public class MfaFoundationStatus
{
    public bool IsFoundationReady { get; set; } = true;
    public string Algorithm { get; set; } = "TOTP-RFC6238";
    public string HashFunction { get; set; } = "HMAC-SHA1";
    public int TimestepSeconds { get; set; } = 30;
    public int CodeDigits { get; set; } = 6;
    public string Description { get; set; } = "Cryptographic RFC 6238 TOTP foundation implemented. Per-user database persistence and mandatory prompt flow is scheduled for future enterprise release.";
}
