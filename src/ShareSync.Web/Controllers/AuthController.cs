using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareSync.Application.Common.Exceptions;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Auth;
using ShareSync.Application.Interfaces;

namespace ShareSync.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMfaService? _mfaService;

    public AuthController(
        IAuthService authService,
        ICurrentUserService currentUserService,
        IMfaService? mfaService = null)
    {
        _authService = authService;
        _currentUserService = currentUserService;
        _mfaService = mfaService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var result = await _authService.RegisterAsync(request, cancellationToken);
        return StatusCode(201, result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var result = await _authService.LoginAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedException();
        }

        var result = await _authService.GetCurrentUserAsync(_currentUserService.UserId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedException();
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var result = await _authService.UpdateProfileAsync(_currentUserService.UserId.Value, request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedException();
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var result = await _authService.ChangePasswordAsync(_currentUserService.UserId.Value, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        // JWT stateless logout: client discards token
        return Ok(ApiResponse.Ok("Logout successful. Client session cleared."));
    }

    [HttpGet("mfa/status")]
    [Authorize]
    public IActionResult GetMfaStatus()
    {
        var status = _mfaService?.GetFoundationStatus() ?? new MfaFoundationStatus();
        return Ok(ApiResponse<MfaFoundationStatus>.Ok(status));
    }

    [HttpPost("mfa/generate")]
    [Authorize]
    public IActionResult GenerateMfaKey()
    {
        if (_mfaService == null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, ApiResponse.Fail("MFA service is not configured."));
        }

        var secret = _mfaService.GenerateSecretKey();
        var email = _currentUserService.Email ?? "investor@sharesync.com";
        var uri = _mfaService.GenerateTotpUri(email, secret);

        var dto = new MfaSetupDto
        {
            SecretKey = secret,
            TotpUri = uri,
            Digits = 6,
            PeriodSeconds = 30,
            Message = "Use this secret key or URI to link an authenticator app (e.g. Google Authenticator). Full mandatory prompt enforcement is scheduled for future enterprise release."
        };

        return Ok(ApiResponse<MfaSetupDto>.Ok(dto));
    }

    [HttpPost("mfa/verify")]
    [Authorize]
    public IActionResult VerifyMfaCode([FromBody] VerifyMfaRequestDto request)
    {
        if (_mfaService == null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, ApiResponse.Fail("MFA service is not configured."));
        }

        var isValid = _mfaService.ValidateTotpCode(request.SecretKey, request.Code);
        var response = new VerifyMfaResponseDto
        {
            IsValid = isValid,
            Message = isValid ? "Code verified successfully." : "Invalid or expired verification code."
        };

        return Ok(ApiResponse<VerifyMfaResponseDto>.Ok(response));
    }
}
