using Microsoft.EntityFrameworkCore;

namespace DataShare.Api;

/// <summary>
/// Contexte EF Core. Aucune entité pour l'instant — les DbSet (User, File, Tag,
/// RefreshToken) seront ajoutés au fil des étapes, au moment de l'implémentation
/// des User Stories correspondantes (voir docs/diagrams/mcd.md pour le MCD cible).
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
