using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DataShare.Api.Controllers;
using DataShare.Api.DTOs;
using DataShare.Api.Exceptions;
using DataShare.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DataShare.UnitTests.Controllers;

[TestClass]
public class FilesControllerTests
{
    private Mock<IFileService> _fileServiceMock = null!;
    private FilesController _controller = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        _fileServiceMock = new Mock<IFileService>();
        _controller = new FilesController(_fileServiceMock.Object);

        // Simule l'utilisateur authentifie que [Authorize] + le middleware JWT poseraient
        // normalement sur HttpContext.User - meme claim (sub) que JwtService.GenerateAccessToken.
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, TestUserId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private static FileRequest CreateRequest() => new()
    {
        File = new Mock<IFormFile>().Object,
        ExpiresInDays = 7
    };

    [TestMethod]
    public async Task UploadFile_WhenUploadSucceeds_ReturnsCreatedWithFileResponse()
    {
        // Arrange
        var request = CreateRequest();
        var fileResponse = new FileResponse
        {
            Id = Guid.NewGuid(),
            Filename = "report.pdf",
            ContentType = "application/pdf",
            SizeBytes = 100,
            DownloadUrl = "http://localhost:4200/download/abc",
            HasPassword = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            Status = "valid",
            Tags = []
        };
        _fileServiceMock.Setup(s => s.UploadAsync(request, TestUserId)).ReturnsAsync(fileResponse);

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        Assert.IsInstanceOfType(result.Result, typeof(ObjectResult));
        var objectResult = (ObjectResult)result.Result!;
        Assert.AreEqual(201, objectResult.StatusCode);
        Assert.AreSame(fileResponse, objectResult.Value);
    }

    [TestMethod]
    public async Task UploadFile_WhenNoFileProvided_ReturnsBadRequestWithMissingFileCode()
    {
        // Arrange
        var request = CreateRequest();
        _fileServiceMock.Setup(s => s.UploadAsync(request, TestUserId)).ThrowsAsync(new MissingFileException());

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        AssertBadRequestWithCode(result, "MISSING_FILE");
    }

    [TestMethod]
    public async Task UploadFile_WhenFileTooLarge_ReturnsBadRequestWithFileTooLargeCode()
    {
        // Arrange
        var request = CreateRequest();
        _fileServiceMock.Setup(s => s.UploadAsync(request, TestUserId)).ThrowsAsync(new FileTooLargeException());

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        AssertBadRequestWithCode(result, "FILE_TOO_LARGE");
    }

    [TestMethod]
    public async Task UploadFile_WhenExpirationInvalid_ReturnsBadRequestWithInvalidExpirationCode()
    {
        // Arrange
        var request = CreateRequest();
        _fileServiceMock.Setup(s => s.UploadAsync(request, TestUserId)).ThrowsAsync(new InvalidExpirationException());

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        AssertBadRequestWithCode(result, "INVALID_EXPIRATION");
    }

    [TestMethod]
    public async Task UploadFile_WhenPasswordTooWeak_ReturnsBadRequestWithWeakFilePasswordCode()
    {
        // Arrange
        var request = CreateRequest();
        _fileServiceMock.Setup(s => s.UploadAsync(request, TestUserId)).ThrowsAsync(new WeakFilePasswordException());

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        AssertBadRequestWithCode(result, "WEAK_FILE_PASSWORD");
    }

    [TestMethod]
    public async Task UploadFile_WhenFileTypeUnsupported_ReturnsBadRequestWithUnsupportedFileTypeCode()
    {
        // Arrange
        var request = CreateRequest();
        _fileServiceMock.Setup(s => s.UploadAsync(request, TestUserId)).ThrowsAsync(new UnsupportedFileTypeException());

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        AssertBadRequestWithCode(result, "UNSUPPORTED_FILE_TYPE");
    }

    private static void AssertBadRequestWithCode(ActionResult<FileResponse> result, string expectedCode)
    {
        Assert.IsInstanceOfType(result.Result, typeof(BadRequestObjectResult));
        var badRequestResult = (BadRequestObjectResult)result.Result!;
        var errorResponse = (ErrorResponse)badRequestResult.Value!;
        Assert.AreEqual(expectedCode, errorResponse.Code);
    }
}
