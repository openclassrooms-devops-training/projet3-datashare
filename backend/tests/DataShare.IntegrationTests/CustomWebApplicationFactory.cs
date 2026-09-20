using DataShare.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DataShare.IntegrationTests;

/// <summary>
/// Demarre l'API reelle (pipeline HTTP complet : routing, [Authorize], middlewares) contre
/// une vraie base Postgres dediee aux tests, differente de la base de dev. La base doit
/// exister au prealable (creee une seule fois, cf. backend/README.md) - EnsureDeletedAsync/
/// MigrateAsync (ResetDatabaseAsync) ne creent que le schema, pas la base elle-meme.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string StorageRootPath { get; } = Path.Combine(Path.GetTempPath(), "datashare-integration-" + Guid.NewGuid());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:DataShare",
            "Host=localhost;Port=5432;Database=datashare_integration_test;Username=datashare;Password=datashare_dev_only");
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-at-least-32-characters-long");
        builder.UseSetting("fileStorage:path", StorageRootPath);
        builder.UseSetting("App:BaseUrl", "http://localhost:4200");
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(StorageRootPath))
        {
            Directory.Delete(StorageRootPath, recursive: true);
        }
    }
}
