namespace ShareSync.Application.DTOs.Auth;

public class MfaSetupDto
{
    public string SecretKey { get; set; } = string.Empty;
    public string TotpUri { get; set; } = string.Empty;
    public int Digits { get; set; } = 6;
    public int PeriodSeconds { get; set; } = 30;
    public string Message { get; set; } = string.Empty;
}

public class VerifyMfaRequestDto
{
    public string SecretKey { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class VerifyMfaResponseDto
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
}
