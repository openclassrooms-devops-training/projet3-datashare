using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DataShare.Api;
using DataShare.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DataShare.IntegrationTests;

[TestClass]
public class FilesIntegrationTests
{
    private static CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    // Signature binaire minimale mais valide d'un PDF (%PDF-...) - suffisante pour passer
    // la detection reelle de Mime-Detective (pas de mock ici, contrairement aux tests unitaires).
    private static readonly byte[] ValidPdfContent = Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF");

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        _factory = new CustomWebApplicationFactory();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _factory.Dispose();
    }

    [TestInitialize]
    public async Task Setup()
    {
        await _factory.ResetDatabaseAsync();
        _client = _factory.CreateClient();
    }

    private async Task<string> RegisterAndLoginAsync()
    {
        var email = $"integration-{Guid.NewGuid()}@example.com";
        const string password = "Password1!";

        await _client.PostAsJsonAsync("/api/auth/register", new { email, password });
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        return tokens!.AccessToken;
    }

    private static MultipartFormDataContent CreateUploadContent(byte[] fileBytes, string fileName = "report.pdf")
    {
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        var content = new MultipartFormDataContent
        {
            { fileContent, "file", fileName }
        };
        return content;
    }

    [TestMethod]
    public async Task UploadFile_WithValidTokenAndFile_ReturnsCreatedAndPersistsEverything()
    {
        // Arrange
        var accessToken = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var content = CreateUploadContent(ValidPdfContent);

        // Act
        var response = await _client.PostAsync("/api/files", content);

        // Assert : reponse HTTP
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var fileResponse = await response.Content.ReadFromJsonAsync<FileResponse>();
        Assert.IsNotNull(fileResponse);
        Assert.AreEqual("report.pdf", fileResponse!.Filename);
        Assert.AreEqual("APPLICATION/PDF", fileResponse.ContentType.ToUpperInvariant());

        // Assert : vraiment persiste en base (pas mocke, vraie requete SQL)
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedFile = await dbContext.Files.SingleAsync();
        Assert.AreEqual("report.pdf", storedFile.OriginalFilename);

        // Assert : vraiment ecrit sur le disque (pas mocke)
        Assert.IsTrue(File.Exists(storedFile.StoragePath));
    }

    [TestMethod]
    public async Task UploadFile_WithoutAuthorizationHeader_ReturnsUnauthorized()
    {
        // Arrange : pas de token attache au client
        var content = CreateUploadContent(ValidPdfContent);

        // Act
        var response = await _client.PostAsync("/api/files", content);

        // Assert : verifie que [Authorize] est reellement applique par le pipeline,
        // pas juste suppose (les tests unitaires ne peuvent pas verifier ca, le
        // controller est instancie directement sans middleware d'authentification)
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task UploadFile_WithUnsupportedFileType_ReturnsBadRequest()
    {
        // Arrange : contenu qui n'a la signature d'aucun type autorise
        var accessToken = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var content = CreateUploadContent(Encoding.ASCII.GetBytes("ceci n'est pas un fichier valide"), "malicious.pdf");

        // Act
        var response = await _client.PostAsync("/api/files", content);

        // Assert : la vraie FileTypeValidationService (Mime-Detective), pas un mock, rejette bien
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.AreEqual("UNSUPPORTED_FILE_TYPE", error!.Code);
    }

    private async Task<FileResponse> UploadFileAsync(string accessToken, byte[]? content = null, string fileName = "report.pdf", string? password = null)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var uploadContent = CreateUploadContent(content ?? ValidPdfContent, fileName);
        if (password != null)
        {
            uploadContent.Add(new StringContent(password), "password");
        }
        var response = await _client.PostAsync("/api/files", uploadContent);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FileResponse>())!;
    }

    private static string ExtractDownloadToken(string downloadUrl) => downloadUrl.Split('/').Last();

    // ---------- GET /api/files (US05) ----------

    [TestMethod]
    public async Task GetFiles_ReturnsOnlyTheAuthenticatedUsersFiles()
    {
        // Arrange
        var ownerToken = await RegisterAndLoginAsync();
        await UploadFileAsync(ownerToken, fileName: "mine.pdf");

        var otherUserToken = await RegisterAndLoginAsync();
        await UploadFileAsync(otherUserToken, fileName: "not-mine.pdf");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // Act
        var response = await _client.GetAsync("/api/files");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var files = await response.Content.ReadFromJsonAsync<List<FileResponse>>();
        Assert.AreEqual(1, files!.Count);
        Assert.AreEqual("mine.pdf", files[0].Filename);
    }

    [TestMethod]
    public async Task GetFiles_WithoutAuthorizationHeader_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/files");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- DELETE /api/files/{id} (US06) ----------

    [TestMethod]
    public async Task DeleteFile_WhenOwnerDeletes_ReturnsNoContentAndRemovesFileFromDiskAndDb()
    {
        // Arrange
        var accessToken = await RegisterAndLoginAsync();
        var uploaded = await UploadFileAsync(accessToken);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storagePath = (await dbContext.Files.SingleAsync()).StoragePath;

        // Act
        var response = await _client.DeleteAsync($"/api/files/{uploaded.Id}");

        // Assert
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.IsFalse(await dbContext.Files.AnyAsync());
        Assert.IsFalse(File.Exists(storagePath));
    }

    [TestMethod]
    public async Task DeleteFile_WhenNotOwner_ReturnsForbiddenAndDoesNotDeleteTheFile()
    {
        // Arrange : reproduit bout en bout (vraie authentification, vraie base) le scenario
        // IDOR corrige sur la suppression - voir SECURITY.md.
        var ownerToken = await RegisterAndLoginAsync();
        var uploaded = await UploadFileAsync(ownerToken);

        var attackerToken = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", attackerToken);

        // Act
        var response = await _client.DeleteAsync($"/api/files/{uploaded.Id}");

        // Assert
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.IsTrue(await dbContext.Files.AnyAsync(f => f.Id == uploaded.Id));
    }

    [TestMethod]
    public async Task DeleteFile_WhenFileDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var accessToken = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Act
        var response = await _client.DeleteAsync($"/api/files/{Guid.NewGuid()}");

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- GET/POST /api/files/download/{token} (US02) ----------

    [TestMethod]
    public async Task GetFileMetadata_WithValidToken_ReturnsMetadataWithoutAuthentication()
    {
        // Arrange
        var accessToken = await RegisterAndLoginAsync();
        var uploaded = await UploadFileAsync(accessToken, fileName: "public.pdf");
        var token = ExtractDownloadToken(uploaded.DownloadUrl);

        // Act : nouveau client sans header Authorization - verifie l'accessibilite publique
        var anonymousClient = _factory.CreateClient();
        var response = await anonymousClient.GetAsync($"/api/files/download/{token}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var metadata = await response.Content.ReadFromJsonAsync<FileMetadataResponse>();
        Assert.AreEqual("public.pdf", metadata!.Filename);
        Assert.IsFalse(metadata.RequiresPassword);
    }

    [TestMethod]
    public async Task GetFileMetadata_WithInvalidToken_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/files/download/token-inexistant");

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task DownloadFile_WithoutPasswordWhenNoneRequired_ReturnsTheActualFileContent()
    {
        // Arrange
        var accessToken = await RegisterAndLoginAsync();
        var uploaded = await UploadFileAsync(accessToken);
        var token = ExtractDownloadToken(uploaded.DownloadUrl);
        var anonymousClient = _factory.CreateClient();

        // Act
        var response = await anonymousClient.PostAsync($"/api/files/download/{token}", content: null);

        // Assert : le flux binaire recu est bien le contenu reellement uploade
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var receivedBytes = await response.Content.ReadAsByteArrayAsync();
        CollectionAssert.AreEqual(ValidPdfContent, receivedBytes);
    }

    [TestMethod]
    public async Task DownloadFile_WithCorrectPassword_ReturnsTheFileContent()
    {
        // Arrange
        var accessToken = await RegisterAndLoginAsync();
        var uploaded = await UploadFileAsync(accessToken, password: "secret123");
        var token = ExtractDownloadToken(uploaded.DownloadUrl);
        var anonymousClient = _factory.CreateClient();

        // Act
        var response = await anonymousClient.PostAsJsonAsync($"/api/files/download/{token}", new { password = "secret123" });

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var receivedBytes = await response.Content.ReadAsByteArrayAsync();
        CollectionAssert.AreEqual(ValidPdfContent, receivedBytes);
    }

    [TestMethod]
    public async Task DownloadFile_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var accessToken = await RegisterAndLoginAsync();
        var uploaded = await UploadFileAsync(accessToken, password: "secret123");
        var token = ExtractDownloadToken(uploaded.DownloadUrl);
        var anonymousClient = _factory.CreateClient();

        // Act
        var response = await anonymousClient.PostAsJsonAsync($"/api/files/download/{token}", new { password = "mauvais-mot-de-passe" });

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
