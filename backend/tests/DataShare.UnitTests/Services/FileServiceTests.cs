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

    // ---------- Helpers communs aux tests US02/US05/US06 ----------

    private DataShare.Api.Models.File AddFileRecord(
        Guid? userId,
        string originalFilename = "doc.pdf",
        DateTime? expiresAt = null,
        string? passwordHash = null,
        bool writePhysicalFile = false,
        string content = "contenu-de-test")
    {
        var downloadToken = Guid.NewGuid().ToString("N");
        var storagePath = Path.Combine(_storagePath, downloadToken + Path.GetExtension(originalFilename));

        if (writePhysicalFile)
        {
            Directory.CreateDirectory(_storagePath);
            File.WriteAllText(storagePath, content);
        }

        var fileRecord = new DataShare.Api.Models.File
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OriginalFilename = originalFilename,
            ContentType = "application/pdf",
            StoragePath = storagePath,
            SizeBytes = content.Length,
            DownloadToken = downloadToken,
            PasswordHash = passwordHash,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Files.Add(fileRecord);
        _dbContext.SaveChanges();
        return fileRecord;
    }

    // ---------- GetFilesForUserAsync (US05) ----------

    [TestMethod]
    public async Task GetFilesForUserAsync_OnlyReturnsFilesBelongingToTheRequestingUser()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        AddFileRecord(TestUserId, "mine.pdf");
        AddFileRecord(otherUserId, "not-mine.pdf");

        // Act
        var result = await _fileService.GetFilesForUserAsync(TestUserId, "all");

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("mine.pdf", result[0].Filename);
    }

    [TestMethod]
    public async Task GetFilesForUserAsync_WithStatusActive_ExcludesExpiredFiles()
    {
        // Arrange
        AddFileRecord(TestUserId, "actif.pdf", expiresAt: DateTime.UtcNow.AddDays(1));
        AddFileRecord(TestUserId, "expire.pdf", expiresAt: DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await _fileService.GetFilesForUserAsync(TestUserId, "active");

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("actif.pdf", result[0].Filename);
    }

    [TestMethod]
    public async Task GetFilesForUserAsync_WithStatusExpired_ExcludesActiveFiles()
    {
        // Arrange
        AddFileRecord(TestUserId, "actif.pdf", expiresAt: DateTime.UtcNow.AddDays(1));
        AddFileRecord(TestUserId, "expire.pdf", expiresAt: DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await _fileService.GetFilesForUserAsync(TestUserId, "expired");

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("expire.pdf", result[0].Filename);
    }

    [TestMethod]
    public async Task GetFilesForUserAsync_IncludesTags()
    {
        // Arrange : regression sur l'oubli de .Include(f => f.Tags) - sans lui, Tags
        // revient toujours vide meme si des tags existent reellement en base.
        var fileRecord = AddFileRecord(TestUserId, "avec-tags.pdf");
        fileRecord.Tags.Add(new DataShare.Api.Models.Tag { Label = "facture" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _fileService.GetFilesForUserAsync(TestUserId, "all");

        // Assert
        CollectionAssert.AreEquivalent(new List<string> { "facture" }, result[0].Tags);
    }

    // ---------- DeleteAsync (US06) ----------

    [TestMethod]
    public async Task DeleteAsync_WhenFileDoesNotExist_ThrowsFileRecordNotFoundException()
    {
        await Assert.ThrowsExceptionAsync<FileRecordNotFoundException>(
            () => _fileService.DeleteAsync(Guid.NewGuid(), TestUserId));
    }

    [TestMethod]
    public async Task DeleteAsync_WhenFileBelongsToAnotherUser_ThrowsFileAccessForbiddenException()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var fileRecord = AddFileRecord(ownerUserId, writePhysicalFile: true);

        // Act & Assert : TestUserId n'est pas le proprietaire
        await Assert.ThrowsExceptionAsync<FileAccessForbiddenException>(
            () => _fileService.DeleteAsync(fileRecord.Id, TestUserId));

        // Le fichier ne doit pas avoir ete supprime malgre la tentative
        Assert.IsTrue(File.Exists(fileRecord.StoragePath));
        Assert.IsNotNull(await _dbContext.Files.FindAsync(fileRecord.Id));
    }

    [TestMethod]
    public async Task DeleteAsync_WhenOwnerDeletes_RemovesDbRowAndPhysicalFile()
    {
        // Arrange
        var fileRecord = AddFileRecord(TestUserId, writePhysicalFile: true);
        var storagePath = fileRecord.StoragePath;

        // Act
        await _fileService.DeleteAsync(fileRecord.Id, TestUserId);

        // Assert
        Assert.IsNull(await _dbContext.Files.FindAsync(fileRecord.Id));
        Assert.IsFalse(File.Exists(storagePath));
    }

    // ---------- GetMetadataByTokenAsync (US02) ----------

    [TestMethod]
    public async Task GetMetadataByTokenAsync_WhenTokenDoesNotExist_ThrowsFileNotFoundOrExpiredException()
    {
        await Assert.ThrowsExceptionAsync<FileNotFoundOrExpiredException>(
            () => _fileService.GetMetadataByTokenAsync("token-inconnu"));
    }

    [TestMethod]
    public async Task GetMetadataByTokenAsync_WhenFileIsExpired_ThrowsFileNotFoundOrExpiredException()
    {
        // Arrange
        var fileRecord = AddFileRecord(TestUserId, expiresAt: DateTime.UtcNow.AddDays(-1));

        // Act & Assert : meme exception que "token inconnu" - ne jamais reveler
        // qu'un lien a existe mais est juste expire (voir SECURITY.md).
        await Assert.ThrowsExceptionAsync<FileNotFoundOrExpiredException>(
            () => _fileService.GetMetadataByTokenAsync(fileRecord.DownloadToken));
    }

    [TestMethod]
    public async Task GetMetadataByTokenAsync_WhenValid_ReturnsMetadataWithoutPasswordHash()
    {
        // Arrange
        var fileRecord = AddFileRecord(TestUserId, "doc.pdf", passwordHash: "un-hash-bcrypt");

        // Act
        var result = await _fileService.GetMetadataByTokenAsync(fileRecord.DownloadToken);

        // Assert
        Assert.AreEqual("doc.pdf", result.Filename);
        Assert.AreEqual("application/pdf", result.ContentType);
        Assert.IsTrue(result.RequiresPassword);
        // FileMetadataResponse n'a meme pas de propriete pour le hash : la seule
        // fuite possible serait un champ ajoute par erreur plus tard - ce test
        // documente l'intention de ne jamais l'exposer.
    }

    [TestMethod]
    public async Task GetMetadataByTokenAsync_WhenNoPassword_RequiresPasswordIsFalse()
    {
        // Arrange
        var fileRecord = AddFileRecord(TestUserId, "doc.pdf", passwordHash: null);

        // Act
        var result = await _fileService.GetMetadataByTokenAsync(fileRecord.DownloadToken);

        // Assert
        Assert.IsFalse(result.RequiresPassword);
    }

    // ---------- DownloadByTokenAsync (US02) ----------

    [TestMethod]
    public async Task DownloadByTokenAsync_WhenTokenDoesNotExist_ThrowsFileNotFoundOrExpiredException()
    {
        await Assert.ThrowsExceptionAsync<FileNotFoundOrExpiredException>(
            () => _fileService.DownloadByTokenAsync("token-inconnu", password: null));
    }

    [TestMethod]
    public async Task DownloadByTokenAsync_WhenFileIsExpired_ThrowsFileNotFoundOrExpiredException()
    {
        // Arrange
        var fileRecord = AddFileRecord(TestUserId, expiresAt: DateTime.UtcNow.AddDays(-1), writePhysicalFile: true);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<FileNotFoundOrExpiredException>(
            () => _fileService.DownloadByTokenAsync(fileRecord.DownloadToken, password: null));
    }

    [TestMethod]
    public async Task DownloadByTokenAsync_WhenPasswordRequiredButMissing_ThrowsInvalidFilePasswordException()
    {
        // Arrange
        var fileRecord = AddFileRecord(TestUserId, passwordHash: BCrypt.Net.BCrypt.HashPassword("secret123"), writePhysicalFile: true);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidFilePasswordException>(
            () => _fileService.DownloadByTokenAsync(fileRecord.DownloadToken, password: null));
    }

    [TestMethod]
    public async Task DownloadByTokenAsync_WhenPasswordIsWrong_ThrowsInvalidFilePasswordException()
    {
        // Arrange
        var fileRecord = AddFileRecord(TestUserId, passwordHash: BCrypt.Net.BCrypt.HashPassword("secret123"), writePhysicalFile: true);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidFilePasswordException>(
            () => _fileService.DownloadByTokenAsync(fileRecord.DownloadToken, password: "mauvais-mot-de-passe"));
    }

    [TestMethod]
    public async Task DownloadByTokenAsync_WhenPasswordIsCorrect_ReturnsFileContent()
    {
        // Arrange
        var fileRecord = AddFileRecord(
            TestUserId, "secret.pdf",
            passwordHash: BCrypt.Net.BCrypt.HashPassword("secret123"),
            writePhysicalFile: true,
            content: "contenu-protege");

        // Act
        var (contentStream, contentType, filename) = await _fileService.DownloadByTokenAsync(fileRecord.DownloadToken, "secret123");

        // Assert
        Assert.AreEqual("application/pdf", contentType);
        Assert.AreEqual("secret.pdf", filename);
        using var reader = new StreamReader(contentStream);
        Assert.AreEqual("contenu-protege", await reader.ReadToEndAsync());
    }

    [TestMethod]
    public async Task DownloadByTokenAsync_WhenNoPasswordRequired_ReturnsFileContentWithoutPassword()
    {
        // Arrange
        var fileRecord = AddFileRecord(TestUserId, "public.pdf", writePhysicalFile: true, content: "contenu-public");

        // Act
        var (contentStream, contentType, filename) = await _fileService.DownloadByTokenAsync(fileRecord.DownloadToken, password: null);

        // Assert
        Assert.AreEqual("public.pdf", filename);
        using var reader = new StreamReader(contentStream);
        Assert.AreEqual("contenu-public", await reader.ReadToEndAsync());
    }
}
