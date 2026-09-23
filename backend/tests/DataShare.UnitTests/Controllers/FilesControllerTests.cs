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

    // ---------- GetFiles (US05) ----------

    [TestMethod]
    public async Task GetFiles_WithDefaultStatus_PassesAllToServiceAndReturnsOk()
    {
        // Arrange
        var files = new List<FileResponse> { new() { Id = Guid.NewGuid(), Filename = "a.pdf" } };
        _fileServiceMock.Setup(s => s.GetFilesForUserAsync(TestUserId, "all")).ReturnsAsync(files);

        // Act
        var result = await _controller.GetFiles();

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        Assert.AreSame(files, okResult.Value);
    }

    [TestMethod]
    public async Task GetFiles_WithExplicitStatus_PassesItToService()
    {
        // Arrange
        _fileServiceMock.Setup(s => s.GetFilesForUserAsync(TestUserId, "expired")).ReturnsAsync([]);

        // Act
        await _controller.GetFiles("expired");

        // Assert
        _fileServiceMock.Verify(s => s.GetFilesForUserAsync(TestUserId, "expired"), Times.Once);
    }

    // ---------- DeleteFile (US06) ----------

    [TestMethod]
    public async Task DeleteFile_WhenSuccessful_ReturnsNoContent()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        _fileServiceMock.Setup(s => s.DeleteAsync(fileId, TestUserId)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteFile(fileId);

        // Assert
        Assert.IsInstanceOfType(result, typeof(NoContentResult));
    }

    [TestMethod]
    public async Task DeleteFile_WhenFileNotFound_ReturnsNotFoundWithCode()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        _fileServiceMock.Setup(s => s.DeleteAsync(fileId, TestUserId)).ThrowsAsync(new FileRecordNotFoundException());

        // Act
        var result = await _controller.DeleteFile(fileId);

        // Assert
        var notFoundResult = (NotFoundObjectResult)result;
        var errorResponse = (ErrorResponse)notFoundResult.Value!;
        Assert.AreEqual("FILE_NOT_FOUND", errorResponse.Code);
    }

    [TestMethod]
    public async Task DeleteFile_WhenNotOwner_ReturnsForbiddenWithCode()
    {
        // Arrange : c'est le test qui protege contre une regression de l'IDOR
        // corrige sur la suppression (voir SECURITY.md) - 403, jamais 404, pour
        // ne pas confondre "pas a toi" et "n'existe pas" cote controller non plus.
        var fileId = Guid.NewGuid();
        _fileServiceMock.Setup(s => s.DeleteAsync(fileId, TestUserId)).ThrowsAsync(new FileAccessForbiddenException());

        // Act
        var result = await _controller.DeleteFile(fileId);

        // Assert
        var objectResult = (ObjectResult)result;
        Assert.AreEqual(403, objectResult.StatusCode);
        var errorResponse = (ErrorResponse)objectResult.Value!;
        Assert.AreEqual("FILE_ACCESS_FORBIDDEN", errorResponse.Code);
    }

    // ---------- GetFileMetadata (US02) ----------

    [TestMethod]
    public async Task GetFileMetadata_WhenFound_ReturnsOkWithMetadata()
    {
        // Arrange
        var metadata = new FileMetadataResponse { Filename = "doc.pdf", ContentType = "application/pdf", SizeBytes = 100, RequiresPassword = false };
        _fileServiceMock.Setup(s => s.GetMetadataByTokenAsync("tok123")).ReturnsAsync(metadata);

        // Act
        var result = await _controller.GetFileMetadata("tok123");

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        Assert.AreSame(metadata, okResult.Value);
    }

    [TestMethod]
    public async Task GetFileMetadata_WhenNotFoundOrExpired_ReturnsNotFoundWithCode()
    {
        // Arrange
        _fileServiceMock.Setup(s => s.GetMetadataByTokenAsync("tok123")).ThrowsAsync(new FileNotFoundOrExpiredException());

        // Act
        var result = await _controller.GetFileMetadata("tok123");

        // Assert
        var notFoundResult = (NotFoundObjectResult)result.Result!;
        var errorResponse = (ErrorResponse)notFoundResult.Value!;
        Assert.AreEqual("FILE_NOT_FOUND_OR_EXPIRED", errorResponse.Code);
    }

    // ---------- DownloadFile (US02) ----------

    [TestMethod]
    public async Task DownloadFile_WhenSuccessful_ReturnsFileStreamResult()
    {
        // Arrange
        using var contentStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("contenu"));
        _fileServiceMock
            .Setup(s => s.DownloadByTokenAsync("tok123", "secret"))
            .ReturnsAsync((contentStream, "application/pdf", "doc.pdf"));

        // Act
        var result = await _controller.DownloadFile("tok123", new DownloadRequest { Password = "secret" });

        // Assert
        var fileResult = (FileStreamResult)result;
        Assert.AreEqual("application/pdf", fileResult.ContentType);
        Assert.AreEqual("doc.pdf", fileResult.FileDownloadName);
    }

    [TestMethod]
    public async Task DownloadFile_WhenNoRequestBody_PassesNullPasswordToService()
    {
        // Arrange : le body est optionnel (fichier sans mot de passe) - verifie qu'on
        // ne plante pas sur un request null et qu'on transmet bien null au service.
        using var contentStream = new MemoryStream();
        _fileServiceMock
            .Setup(s => s.DownloadByTokenAsync("tok123", null))
            .ReturnsAsync((contentStream, "application/pdf", "doc.pdf"));

        // Act
        var result = await _controller.DownloadFile("tok123", request: null);

        // Assert
        Assert.IsInstanceOfType(result, typeof(FileStreamResult));
        _fileServiceMock.Verify(s => s.DownloadByTokenAsync("tok123", null), Times.Once);
    }

    [TestMethod]
    public async Task DownloadFile_WhenNotFoundOrExpired_ReturnsNotFoundWithCode()
    {
        // Arrange
        _fileServiceMock.Setup(s => s.DownloadByTokenAsync("tok123", null)).ThrowsAsync(new FileNotFoundOrExpiredException());

        // Act
        var result = await _controller.DownloadFile("tok123", request: null);

        // Assert
        var notFoundResult = (NotFoundObjectResult)result;
        var errorResponse = (ErrorResponse)notFoundResult.Value!;
        Assert.AreEqual("FILE_NOT_FOUND_OR_EXPIRED", errorResponse.Code);
    }

    [TestMethod]
    public async Task DownloadFile_WhenPasswordInvalid_ReturnsUnauthorizedWithCode()
    {
        // Arrange
        _fileServiceMock.Setup(s => s.DownloadByTokenAsync("tok123", "faux")).ThrowsAsync(new InvalidFilePasswordException());

        // Act
        var result = await _controller.DownloadFile("tok123", new DownloadRequest { Password = "faux" });

        // Assert
        var unauthorizedResult = (UnauthorizedObjectResult)result;
        var errorResponse = (ErrorResponse)unauthorizedResult.Value!;
        Assert.AreEqual("INVALID_FILE_PASSWORD", errorResponse.Code);
    }
}
