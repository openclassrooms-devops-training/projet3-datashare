using DataShare.Api.Controllers;
using DataShare.Api.DTOs;
using DataShare.Api.Exceptions;
using DataShare.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DataShare.UnitTests.Controllers;

[TestClass]
public class AuthControllerTests
{
    private Mock<IAuthService> _authServiceMock = null!;
    private AuthController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        _authServiceMock = new Mock<IAuthService>();
        _controller = new AuthController(_authServiceMock.Object);
    }

    [TestMethod]
    public async Task Login_WhenCredentialsAreValid_ReturnsOk()
    {
        // Arrange
        var request = new LoginRequest { Email = "test@example.com", Password = "Password1!" };
        var tokenResponse = new TokenResponse { AccessToken = "jwt-token", RefreshToken = "refresh-token", ExpiresIn = 1800 };
        _authServiceMock.Setup(s => s.LoginAsync(request)).ReturnsAsync(tokenResponse);

        // Act
        var result = await _controller.Login(request);

        // Assert
        Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult));
        var okResult = (OkObjectResult)result.Result!;
        Assert.AreSame(tokenResponse, okResult.Value);
    }

    [TestMethod]
    public async Task Login_WhenCredentialsAreInvalid_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest { Email = "test@example.com", Password = "wrong-password" };
        _authServiceMock.Setup(s => s.LoginAsync(request)).ThrowsAsync(new InvalidCredentialsException());

        // Act
        var result = await _controller.Login(request);

        // Assert
        Assert.IsInstanceOfType(result.Result, typeof(UnauthorizedObjectResult));
        var unauthorizedResult = (UnauthorizedObjectResult)result.Result!;
        var errorResponse = (ErrorResponse)unauthorizedResult.Value!;
        Assert.AreEqual("INVALID_CREDENTIALS", errorResponse.Code);
    }

    [TestMethod]
    public async Task Register_WhenEmailIsNew_ReturnsCreated()
    {
        // Arrange
        var request = new RegisterRequest { Email = "new@example.com", Password = "Password1!" };
        var userResponse = new UserResponse { Id = Guid.NewGuid(), Email = "new@example.com", CreatedAt = DateTime.UtcNow };
        _authServiceMock.Setup(s => s.RegisterAsync(request)).ReturnsAsync(userResponse);

        // Act
        var result = await _controller.Register(request);

        // Assert
        Assert.IsInstanceOfType(result.Result, typeof(ObjectResult));
        var objectResult = (ObjectResult)result.Result!;
        Assert.AreEqual(201, objectResult.StatusCode);
        Assert.AreSame(userResponse, objectResult.Value);
    }

    [TestMethod]
    public async Task Register_WhenEmailAlreadyUsed_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest { Email = "existing@example.com", Password = "Password1!" };
        _authServiceMock.Setup(s => s.RegisterAsync(request)).ThrowsAsync(new EmailAlreadyUsedException());

        // Act
        var result = await _controller.Register(request);

        // Assert
        Assert.IsInstanceOfType(result.Result, typeof(BadRequestObjectResult));
        var badRequestResult = (BadRequestObjectResult)result.Result!;
        var errorResponse = (ErrorResponse)badRequestResult.Value!;
        Assert.AreEqual("EMAIL_ALREADY_USED", errorResponse.Code);
    }

    [TestMethod]
    public async Task RefreshToken_WhenTokenIsValid_ReturnsOk()
    {
        // Arrange
        var request = new RefreshRequest { RefreshToken = "valid-refresh-token" };
        var tokenResponse = new TokenResponse { AccessToken = "new-jwt-token", RefreshToken = "new-refresh-token", ExpiresIn = 1800 };
        _authServiceMock.Setup(s => s.RefreshTokenAsync(request.RefreshToken)).ReturnsAsync(tokenResponse);

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult));
        var okResult = (OkObjectResult)result.Result!;
        Assert.AreSame(tokenResponse, okResult.Value);
    }

    [TestMethod]
    public async Task RefreshToken_WhenTokenIsInvalid_ReturnsUnauthorized()
    {
        // Arrange
        var request = new RefreshRequest { RefreshToken = "invalid-refresh-token" };
        _authServiceMock.Setup(s => s.RefreshTokenAsync(request.RefreshToken)).ThrowsAsync(new InvalidRefreshTokenException());

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        Assert.IsInstanceOfType(result.Result, typeof(UnauthorizedObjectResult));
        var unauthorizedResult = (UnauthorizedObjectResult)result.Result!;
        var errorResponse = (ErrorResponse)unauthorizedResult.Value!;
        Assert.AreEqual("INVALID_REFRESH_TOKEN", errorResponse.Code);
    }

    [TestMethod]
    public async Task Logout_WhenTokenIsValid_ReturnsNoContent()
    {
        // Arrange
        var request = new RefreshRequest { RefreshToken = "valid-refresh-token" };
        _authServiceMock.Setup(s => s.LogoutAsync(request.RefreshToken)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Logout(request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(NoContentResult));
    }

    [TestMethod]
    public async Task Logout_WhenTokenIsInvalid_ReturnsUnauthorized()
    {
        // Arrange
        var request = new RefreshRequest { RefreshToken = "invalid-refresh-token" };
        _authServiceMock.Setup(s => s.LogoutAsync(request.RefreshToken)).ThrowsAsync(new InvalidRefreshTokenException());

        // Act
        var result = await _controller.Logout(request);

        // Assert
        Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult));
        var unauthorizedResult = (UnauthorizedObjectResult)result;
        var errorResponse = (ErrorResponse)unauthorizedResult.Value!;
        Assert.AreEqual("INVALID_REFRESH_TOKEN", errorResponse.Code);
    }
}
