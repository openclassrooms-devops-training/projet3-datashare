using System.Security.Cryptography;
using System.Text;
using DataShare.Api.DTOs;
using DataShare.Api.Exceptions;
using DataShare.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DataShare.Api.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public AuthService(AppDbContext dbContext, IJwtService jwtService)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
    }

    public async Task<UserResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingUser != null)
        {
            throw new EmailAlreadyUsedException();
        }
 
        var newUser = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        };

        _dbContext.Users.Add(newUser);
        await _dbContext.SaveChangesAsync();

        return new UserResponse
        {
            Id = newUser.Id,
            Email = newUser.Email
        };
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request)
    {
        var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingUser == null)
        {
            throw new InvalidCredentialsException();
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, existingUser.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        //generate an access token  
        var token = _jwtService.GenerateAccessToken(existingUser);

        //generate a refresh token
        var refreshToken = _jwtService.GenerateRefreshToken();
        string refreshTokenHash = HashRefreshToken(refreshToken);

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = refreshTokenHash
                                      ,
            UserId = existingUser.Id
                                      ,
            CreatedAt = DateTime.UtcNow
                                      ,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
                                      ,
            Revoked = false
        });
        await _dbContext.SaveChangesAsync();

        return new TokenResponse
        {
            AccessToken = token,
            RefreshToken = refreshToken,
            ExpiresIn = 30 * 60 // 30 minutes in seconds
        };


    }

    private static string HashRefreshToken(string refreshToken)
    {
        //store the refresh token in the database
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        var refreshTokenHash = Convert.ToBase64String(hashBytes);
        return refreshTokenHash;
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken)
    {
        //search refresh token in database
        string tokenHash = HashRefreshToken(refreshToken);
        var refreshTokenEntity = await _dbContext.RefreshTokens
                                                 .Include(rt => rt.User)
                                                 .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (string.IsNullOrEmpty(refreshToken) || refreshTokenEntity == null)
        {
            throw new InvalidRefreshTokenException();
        }

        if (refreshTokenEntity.Revoked)
        {
            // signal de vol : quelqu'un represente un token deja consomme donc je revoke toutes les autres tokens non encore revokes
            await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == refreshTokenEntity.UserId && !rt.Revoked)
                .ExecuteUpdateAsync(setters => setters.SetProperty(rt => rt.Revoked, true));

            throw new InvalidRefreshTokenException(); // meme message qu'un cas normal, cote client
        }

        if (refreshTokenEntity.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidRefreshTokenException();
        }

        //revoke the old refresh token
        refreshTokenEntity.Revoked = true;

        //generate a new refresh token
        var newRefreshToken = _jwtService.GenerateRefreshToken();
        string newRefreshTokenHash = HashRefreshToken(newRefreshToken);

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = newRefreshTokenHash,
            UserId = refreshTokenEntity.UserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Revoked = false
        });

        await _dbContext.SaveChangesAsync();

        return new TokenResponse
        {
            AccessToken = _jwtService.GenerateAccessToken(refreshTokenEntity.User),
            RefreshToken = newRefreshToken,
            ExpiresIn = 30 * 60 // 30 minutes in seconds
        };

    }

    public async Task LogoutAsync(string refreshToken)
    {
        //search refresh token in database
        string tokenHash = HashRefreshToken(refreshToken);
        var refreshTokenEntity = await _dbContext.RefreshTokens
                                                 .Include(rt => rt.User)
                                                 .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (string.IsNullOrEmpty(refreshToken) || refreshTokenEntity == null)
        {
            throw new InvalidRefreshTokenException();
        }

        //revoke the refresh token
        refreshTokenEntity.Revoked = true;
        await _dbContext.SaveChangesAsync();
    }
}
