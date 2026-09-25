using System.Security.Claims;
using DataShare.Api.Models;

namespace DataShare.Api.Services;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateAccessToken(string token);
}