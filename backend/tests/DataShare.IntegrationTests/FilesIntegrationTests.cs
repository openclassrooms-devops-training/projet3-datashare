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
}
