using ShareSync.Domain.Entities;

namespace ShareSync.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateToken(AppUser user);
}
