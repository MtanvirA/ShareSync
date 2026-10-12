using System.Security.Cryptography;
using System.Text;
using ShareSync.Application.Common.Interfaces;

namespace ShareSync.Infrastructure.Security;

public class TotpMfaService : IMfaService
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int TimestepSeconds = 30;
    private const int CodeDigits = 6;

    public string GenerateSecretKey(int byteLength = 20)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return ToBase32String(bytes);
    }

    public string GenerateTotpUri(string email, string secretKey, string issuer = "ShareSync")
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(email);
        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secretKey}&issuer={encodedIssuer}&algorithm=SHA1&digits={CodeDigits}&period={TimestepSeconds}";
    }

    public bool ValidateTotpCode(string secretKey, string code, int allowedDriftSteps = 1, DateTime? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalizedCode = code.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        if (normalizedCode.Length != CodeDigits || !int.TryParse(normalizedCode, out _))
        {
            return false;
        }

        byte[] secretBytes;
        try
        {
            secretBytes = FromBase32String(secretKey.Trim());
        }
        catch
        {
            return false;
        }

        var time = timestamp ?? DateTime.UtcNow;
        var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var totalSeconds = (long)(time.ToUniversalTime() - unixEpoch).TotalSeconds;
        var currentStep = totalSeconds / TimestepSeconds;

        for (var i = -allowedDriftSteps; i <= allowedDriftSteps; i++)
        {
            var step = currentStep + i;
            var expectedCode = ComputeTotpForStep(secretBytes, step);

            if (CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(normalizedCode),
                Encoding.UTF8.GetBytes(expectedCode)))
            {
                return true;
            }
        }

        return false;
    }

    public MfaFoundationStatus GetFoundationStatus()
    {
        return new MfaFoundationStatus();
    }

    private static string ComputeTotpForStep(byte[] secret, long step)
    {
        var stepBytes = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(stepBytes);
        }

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(stepBytes);

        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var code = binary % (int)Math.Pow(10, CodeDigits);
        return code.ToString(new string('0', CodeDigits));
    }

    private static string ToBase32String(byte[] data)
    {
        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int currentByte = 0, currentBitIndex = 0;

        for (var i = 0; i < data.Length; i++)
        {
            currentByte = (currentByte << 8) | data[i];
            currentBitIndex += 8;

            while (currentBitIndex >= 5)
            {
                currentBitIndex -= 5;
                var charIndex = (currentByte >> currentBitIndex) & 0x1F;
                result.Append(Base32Alphabet[charIndex]);
            }
        }

        if (currentBitIndex > 0)
        {
            var charIndex = (currentByte << (5 - currentBitIndex)) & 0x1F;
            result.Append(Base32Alphabet[charIndex]);
        }

        return result.ToString();
    }

    private static byte[] FromBase32String(string input)
    {
        var cleanInput = input.Trim().ToUpperInvariant().Replace("=", string.Empty);
        var output = new List<byte>((cleanInput.Length * 5) / 8);

        int buffer = 0, bitsInBuffer = 0;

        foreach (var c in cleanInput)
        {
            var charIndex = Base32Alphabet.IndexOf(c);
            if (charIndex < 0)
            {
                throw new ArgumentException($"Invalid Base32 character: {c}", nameof(input));
            }

            buffer = (buffer << 5) | charIndex;
            bitsInBuffer += 5;

            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                output.Add((byte)((buffer >> bitsInBuffer) & 0xFF));
            }
        }

        return output.ToArray();
    }
}
