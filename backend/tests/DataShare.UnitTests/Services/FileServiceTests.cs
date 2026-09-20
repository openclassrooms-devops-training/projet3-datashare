using DataShare.Api;
using DataShare.Api.DTOs;
using DataShare.Api.Exceptions;
using DataShare.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataShare.UnitTests.Services;

[TestClass]
public class FileServiceTests
{
    private AppDbContext _dbContext = null!;
    private Mock<IDownloadToken> _downloadTokenMock = null!;
    private Mock<IFileTypeValidationService> _fileTypeValidationServiceMock = null!;
    private Mock<IConfiguration> _configurationMock = null!;
    private FileService _fileService = null!;
    private string _storagePath = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _storagePath = Path.Combine(Path.GetTempPath(), "datashare-tests-" + Guid.NewGuid());

        _downloadTokenMock = new Mock<IDownloadToken>();
        _downloadTokenMock.Setup(d => d.GenerateTokenAsync(It.IsAny<Guid>())).ReturnsAsync("test-download-token");

        _fileTypeValidationServiceMock = new Mock<IFileTypeValidationService>();

        _configurationMock = new Mock<IConfiguration>();
        _configurationMock.Setup(c => c["fileStorage:path"]).Returns(_storagePath);
        _configurationMock.Setup(c => c["App:BaseUrl"]).Returns("http://localhost:4200");

        _fileService = new FileService(
            _dbContext,
            _downloadTokenMock.Object,
            _configurationMock.Object,
            _fileTypeValidationServiceMock.Object,
            NullLogger<FileService>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(_storagePath, recursive: true);
        }
    }

    private static Mock<IFormFile> CreateFakeFile(string fileName, long length)
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(length);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return fileMock;
    }

    [TestMethod]
    public async Task UploadAsync_WhenFileIsNull_ThrowsMissingFileException()
    {
        // Arrange
        var request = new FileRequest { File = null!, ExpiresInDays = 7 };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<MissingFileException>(() => _fileService.UploadAsync(request, TestUserId));
    }

    [TestMethod]
    public async Task UploadAsync_WhenFileIsEmpty_ThrowsMissingFileException()
    {
        // Arrange
        var request = new FileRequest { File = CreateFakeFile("empty.pdf", 0).Object, ExpiresInDays = 7 };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<MissingFileException>(() => _fileService.UploadAsync(request, TestUserId));
    }

    [TestMethod]
    public async Task UploadAsync_WhenFileExceeds1Gb_ThrowsFileTooLargeException()
    {
        // Arrange
        var request = new FileRequest
        {
            File = CreateFakeFile("big.pdf", 1024L * 1024 * 1024 + 1).Object,
            ExpiresInDays = 7
        };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<FileTooLargeException>(() => _fileService.UploadAsync(request, TestUserId));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(8)]
    public async Task UploadAsync_WhenExpiresInDaysIsOutOfRange_ThrowsInvalidExpirationException(int expiresInDays)
    {
        // Arrange
        var request = new FileRequest
        {
            File = CreateFakeFile("doc.pdf", 100).Object,
            ExpiresInDays = expiresInDays
        };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidExpirationException>(() => _fileService.UploadAsync(request, TestUserId));
    }

    [TestMethod]
    public async Task UploadAsync_WhenPasswordIsTooShort_ThrowsWeakFilePasswordException()
    {
        // Arrange
        var request = new FileRequest
        {
            File = CreateFakeFile("doc.pdf", 100).Object,
            ExpiresInDays = 7,
            Password = "abc"
        };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<WeakFilePasswordException>(() => _fileService.UploadAsync(request, TestUserId));
    }

    [TestMethod]
    public async Task UploadAsync_WhenFileTypeIsUnsupported_ThrowsUnsupportedFileTypeExceptionAndDeletesOrphanFile()
    {
        // Arrange
        _fileTypeValidationServiceMock
            .Setup(v => v.DetectAndValidateAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync((string?)null);
        var request = new FileRequest
        {
            File = CreateFakeFile("malicious.pdf", 100).Object,
            ExpiresInDays = 7
        };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<UnsupportedFileTypeException>(() => _fileService.UploadAsync(request, TestUserId));

        // Assert : pas de fichier orphelin laisse sur le disque apres le rejet
        var expectedPath = Path.Combine(_storagePath, "test-download-token.pdf");
        Assert.IsFalse(File.Exists(expectedPath));
    }

    [TestMethod]
    public async Task UploadAsync_WhenValid_ReturnsFileResponseAndPersistsEntity()
    {
        // Arrange
        _fileTypeValidationServiceMock
            .Setup(v => v.DetectAndValidateAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("application/pdf");
        var request = new FileRequest
        {
            File = CreateFakeFile("report.pdf", 1234).Object,
            ExpiresInDays = 3
        };

        // Act
        var result = await _fileService.UploadAsync(request, TestUserId);

        // Assert
        Assert.AreEqual("report.pdf", result.Filename);
        Assert.AreEqual("application/pdf", result.ContentType);
        Assert.AreEqual(1234, result.SizeBytes);
        Assert.AreEqual(false, result.HasPassword);
        Assert.AreEqual("valid", result.Status);
        Assert.AreEqual("http://localhost:4200/download/test-download-token", result.DownloadUrl);

        var storedFile = await _dbContext.Files.SingleAsync();
        Assert.AreEqual(TestUserId, storedFile.UserId);
        Assert.AreEqual("report.pdf", storedFile.OriginalFilename);
        Assert.IsNull(storedFile.PasswordHash);
        Assert.IsTrue(File.Exists(storedFile.StoragePath));
    }

    [TestMethod]
    public async Task UploadAsync_WhenPasswordProvided_HashesPasswordAndSetsHasPasswordTrue()
    {
        // Arrange
        _fileTypeValidationServiceMock
            .Setup(v => v.DetectAndValidateAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("application/pdf");
        var request = new FileRequest
        {
            File = CreateFakeFile("report.pdf", 100).Object,
            ExpiresInDays = 7,
            Password = "secret123"
        };

        // Act
        var result = await _fileService.UploadAsync(request, TestUserId);

        // Assert
        Assert.IsTrue(result.HasPassword);
        var storedFile = await _dbContext.Files.SingleAsync();
        Assert.IsNotNull(storedFile.PasswordHash);
        Assert.AreNotEqual("secret123", storedFile.PasswordHash);
        Assert.IsTrue(BCrypt.Net.BCrypt.Verify("secret123", storedFile.PasswordHash));
    }

    [TestMethod]
    public async Task UploadAsync_WhenTagsProvided_PersistsTags()
    {
        // Arrange
        _fileTypeValidationServiceMock
            .Setup(v => v.DetectAndValidateAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("application/pdf");
        var request = new FileRequest
        {
            File = CreateFakeFile("report.pdf", 100).Object,
            ExpiresInDays = 7,
            Tags = new List<string> { "facture", "2026" }
        };

        // Act
        var result = await _fileService.UploadAsync(request, TestUserId);

        // Assert
        CollectionAssert.AreEquivalent(new List<string> { "facture", "2026" }, result.Tags);
        var storedFile = await _dbContext.Files.Include(f => f.Tags).SingleAsync();
        CollectionAssert.AreEquivalent(new List<string> { "facture", "2026" }, storedFile.Tags.Select(t => t.Label).ToList());
    }
}
