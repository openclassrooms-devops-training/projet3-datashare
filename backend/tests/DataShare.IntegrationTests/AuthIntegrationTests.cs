using System.Net;
using System.Net.Http.Json;
using DataShare.Api.DTOs;

namespace DataShare.IntegrationTests;

[TestClass]
public class AuthIntegrationTests
{
    private static CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

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

    private async Task<(string Email, string Password)> RegisterAndReturnCredentialsAsync()
    {
        var email = $"integration-{Guid.NewGuid()}@example.com";
        const string password = "Password1!";

        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        return (email, password);
    }

    [TestMethod]
    public async Task Register_Login_Refresh_FullFlow_Succeeds()
    {
        // Arrange
        var (email, password) = await RegisterAndReturnCredentialsAsync();

        // Act : login
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });

        // Assert : login
        Assert.AreEqual(HttpStatusCode.OK, loginResponse.StatusCode);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.IsNotNull(tokens);
        Assert.IsFalse(string.IsNullOrEmpty(tokens!.AccessToken));
        Assert.IsFalse(string.IsNullOrEmpty(tokens.RefreshToken));

        // Act : refresh
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });

        // Assert : refresh - un vrai nouveau couple de tokens, different de l'original
        Assert.AreEqual(HttpStatusCode.OK, refreshResponse.StatusCode);
        var newTokens = await refreshResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.IsNotNull(newTokens);
        Assert.AreNotEqual(tokens.RefreshToken, newTokens!.RefreshToken);
    }

    [TestMethod]
    public async Task RefreshToken_WhenRevokedTokenIsReplayed_RevokesAllActiveTokensForTheUser()
    {
        // Arrange : login puis refresh une fois - le refresh token original devient Revoked=true
        var (email, password) = await RegisterAndReturnCredentialsAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var originalTokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var firstRefreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = originalTokens!.RefreshToken });
        var rotatedTokens = await firstRefreshResponse.Content.ReadFromJsonAsync<TokenResponse>();

        // Act : rejouer le refresh token original (deja revoque) - signal de vol
        var replayResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = originalTokens.RefreshToken });

        // Assert : le rejeu echoue
        Assert.AreEqual(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        // Assert : le token legitime issu de la rotation (rotatedTokens) a lui aussi ete
        // revoque en cascade - meme s'il n'a jamais ete compromis directement. C'est le cas
        // qui ne pouvait pas etre teste en unitaire (EF Core InMemory ne supporte pas
        // ExecuteUpdateAsync, utilise par AuthService pour la revocation en masse).
        var cascadeCheckResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = rotatedTokens!.RefreshToken });
        Assert.AreEqual(HttpStatusCode.Unauthorized, cascadeCheckResponse.StatusCode);
    }

    [TestMethod]
    public async Task Register_WhenEmailAlreadyUsed_ReturnsBadRequest()
    {
        // Arrange
        var (email, password) = await RegisterAndReturnCredentialsAsync();

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email, password });

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.AreEqual("EMAIL_ALREADY_USED", error!.Code);
    }
}
