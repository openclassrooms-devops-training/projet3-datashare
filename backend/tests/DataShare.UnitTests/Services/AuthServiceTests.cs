using System.Security.Cryptography;
using System.Text;
using DataShare.Api;
using DataShare.Api.DTOs;
using DataShare.Api.Exceptions;
using DataShare.Api.Models;
using DataShare.Api.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DataShare.UnitTests.Services;

[TestClass]
public class AuthServiceTests
{
    private AppDbContext _dbContext = null!;
    private Mock<IJwtService> _jwtServiceMock = null!;
    private AuthService _authService = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
        _jwtServiceMock = new Mock<IJwtService>();
        _authService = new AuthService(_dbContext, _jwtServiceMock.Object);
    }

    // Reproduction du hash prive de AuthService.HashRefreshToken, pour preparer/verifier les donnees en base dans les tests.
    private static string HashRefreshToken(string refreshToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(hashBytes);
    }

    [TestMethod]
    public async Task RegisterAsync_WhenEmailIsNew_CreatesUserAndReturnsUserResponse()
    {
        // Arrange
        var request = new RegisterRequest { Email = "new@example.com", Password = "Password1!" };

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.AreEqual("new@example.com", result.Email);
        var storedUser = await _dbContext.Users.SingleAsync();
        Assert.AreEqual("new@example.com", storedUser.Email);
        Assert.AreNotEqual("Password1!", storedUser.PasswordHash);
        Assert.IsTrue(BCrypt.Net.BCrypt.Verify("Password1!", storedUser.PasswordHash));
    }

    [TestMethod]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ThrowsEmailAlreadyUsedException()
    {
        // Arrange
        _dbContext.Users.Add(new User { Email = "existing@example.com", PasswordHash = "irrelevant" });
        await _dbContext.SaveChangesAsync();
        var request = new RegisterRequest { Email = "existing@example.com", Password = "Password1!" };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<EmailAlreadyUsedException>(() => _authService.RegisterAsync(request));
    }

    [TestMethod]
    public async Task LoginAsync_WhenUserDoesNotExist_ThrowsInvalidCredentialsException()
    {
        // Arrange
        var request = new LoginRequest { Email = "unknown@example.com", Password = "Password1!" };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidCredentialsException>(() => _authService.LoginAsync(request));
    }

    [TestMethod]
    public async Task LoginAsync_WhenPasswordIsWrong_ThrowsInvalidCredentialsException()
    {
        // Arrange
        _dbContext.Users.Add(new User
        {
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword1!")
        });
        await _dbContext.SaveChangesAsync();
        var request = new LoginRequest { Email = "test@example.com", Password = "WrongPassword1!" };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidCredentialsException>(() => _authService.LoginAsync(request));
    }

    [TestMethod]
    public async Task LoginAsync_WhenCredentialsAreValid_ReturnsTokenResponseAndStoresRefreshToken()
    {
        // Arrange
        var user = new User { Email = "test@example.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!") };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        _jwtServiceMock.Setup(j => j.GenerateAccessToken(It.Is<User>(u => u.Email == user.Email))).Returns("access-token");
        _jwtServiceMock.Setup(j => j.GenerateRefreshToken()).Returns("refresh-token-plain");
        var request = new LoginRequest { Email = "test@example.com", Password = "Password1!" };

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.AreEqual("access-token", result.AccessToken);
        Assert.AreEqual("refresh-token-plain", result.RefreshToken);
        Assert.AreEqual(1800, result.ExpiresIn);

        var storedToken = await _dbContext.RefreshTokens.SingleAsync();
        Assert.AreEqual(user.Id, storedToken.UserId);
        Assert.AreEqual(HashRefreshToken("refresh-token-plain"), storedToken.TokenHash);
        Assert.IsFalse(storedToken.Revoked);
    }

    [TestMethod]
    public async Task RefreshTokenAsync_WhenTokenIsEmpty_ThrowsInvalidRefreshTokenException()
    {
        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidRefreshTokenException>(() => _authService.RefreshTokenAsync(""));
    }

    [TestMethod]
    public async Task RefreshTokenAsync_WhenTokenIsNotFound_ThrowsInvalidRefreshTokenException()
    {
        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidRefreshTokenException>(() => _authService.RefreshTokenAsync("does-not-exist"));
    }

    // Pas de test pour le cas "token revoke presente" (signal de vol, cf. AuthService.RefreshTokenAsync) :
    // ce chemin utilise ExecuteUpdateAsync, non supporte par le provider EF Core InMemory (relationnel
    // uniquement). A couvrir plutot cote DataShare.IntegrationTests, sur une vraie base.

    [TestMethod]
    public async Task RefreshTokenAsync_WhenTokenIsExpired_ThrowsInvalidRefreshTokenException()
    {
        // Arrange
        var user = new User { Email = "test@example.com", PasswordHash = "irrelevant" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashRefreshToken("expired-token"),
            Revoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        });
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidRefreshTokenException>(() => _authService.RefreshTokenAsync("expired-token"));
    }

    [TestMethod]
    public async Task RefreshTokenAsync_WhenTokenIsValid_ReturnsNewTokenResponseAndRevokesOldToken()
    {
        // Arrange
        var user = new User { Email = "test@example.com", PasswordHash = "irrelevant" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        var oldToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashRefreshToken("old-token"),
            Revoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _dbContext.RefreshTokens.Add(oldToken);
        await _dbContext.SaveChangesAsync();
        _jwtServiceMock.Setup(j => j.GenerateAccessToken(It.Is<User>(u => u.Id == user.Id))).Returns("new-access-token");
        _jwtServiceMock.Setup(j => j.GenerateRefreshToken()).Returns("new-refresh-token-plain");

        // Act
        var result = await _authService.RefreshTokenAsync("old-token");

        // Assert
        Assert.AreEqual("new-access-token", result.AccessToken);
        Assert.AreEqual("new-refresh-token-plain", result.RefreshToken);

        var refreshedOldToken = await _dbContext.RefreshTokens.SingleAsync(rt => rt.Id == oldToken.Id);
        Assert.IsTrue(refreshedOldToken.Revoked);

        var newToken = await _dbContext.RefreshTokens.SingleAsync(rt => rt.Id != oldToken.Id);
        Assert.AreEqual(HashRefreshToken("new-refresh-token-plain"), newToken.TokenHash);
        Assert.IsFalse(newToken.Revoked);
    }

    [TestMethod]
    public async Task LogoutAsync_WhenTokenIsEmpty_ThrowsInvalidRefreshTokenException()
    {
        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidRefreshTokenException>(() => _authService.LogoutAsync(""));
    }

    [TestMethod]
    public async Task LogoutAsync_WhenTokenIsNotFound_ThrowsInvalidRefreshTokenException()
    {
        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidRefreshTokenException>(() => _authService.LogoutAsync("does-not-exist"));
    }

    [TestMethod]
    public async Task LogoutAsync_WhenTokenIsValid_RevokesToken()
    {
        // Arrange
        var user = new User { Email = "test@example.com", PasswordHash = "irrelevant" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        var token = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashRefreshToken("active-token"),
            Revoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        // Act
        await _authService.LogoutAsync("active-token");

        // Assert
        var refreshedToken = await _dbContext.RefreshTokens.SingleAsync(rt => rt.Id == token.Id);
        Assert.IsTrue(refreshedToken.Revoked);
    }
}
