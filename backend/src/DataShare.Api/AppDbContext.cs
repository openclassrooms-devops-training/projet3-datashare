using DataShare.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataShare.Api;

/// <summary>
/// Contexte EF Core. DbSet ajoutés au fil des étapes, au moment de l'implémentation
/// des User Stories correspondantes (voir docs/diagrams/mcd.md pour le MCD cible).
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId);
    }
}
