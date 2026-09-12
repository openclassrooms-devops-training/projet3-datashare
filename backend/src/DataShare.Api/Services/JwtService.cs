using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DataShare.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace DataShare.Api.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key manquant dans la configuration");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    }

    public string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
                    {
                        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                        new Claim(JwtRegisteredClaimNames.Email, user.Email)
                    };

        var creds = new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        // Generate a secure random refresh token
        byte[] randomBytes = RandomNumberGenerator.GetBytes(64);
        string refreshToken = Convert.ToBase64String(randomBytes);

        return refreshToken;
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        // MapInboundClaims = false : evite que JwtSecurityTokenHandler ne renomme
        // silencieusement les claims standards (ex. "sub" -> ClaimTypes.NameIdentifier)
        // vers d'anciens identifiants .NET/WS-Federation. Sans ca, relire un claim avec
        // JwtRegisteredClaimNames.Sub echouerait alors qu'il a bien ete ecrit avec ce nom
        // dans GenerateAccessToken.
        var tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = GetSigningKey(),
                ValidateIssuer = false,
                ValidateAudience = false
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }
}