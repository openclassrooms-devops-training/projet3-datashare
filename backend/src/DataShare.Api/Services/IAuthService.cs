using DataShare.Api.DTOs;

namespace DataShare.Api.Services;

public interface IAuthService
{
    Task<UserResponse> RegisterAsync(RegisterRequest request);
    Task<TokenResponse> LoginAsync(LoginRequest request);
    Task<TokenResponse> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
}
