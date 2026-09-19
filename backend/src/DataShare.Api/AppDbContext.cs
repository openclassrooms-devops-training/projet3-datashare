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

    public DbSet<Models.File> Files => Set<DataShare.Api.Models.File>();

    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId);

        modelBuilder.Entity<Models.File>()
            .HasIndex(f => f.DownloadToken)
            .IsUnique();

        modelBuilder.Entity<Models.File>()
        .HasOne(f => f.User)
        .WithMany()
        .HasForeignKey(f => f.UserId)
        .IsRequired(false);

        modelBuilder.Entity<Tag>()
            .HasOne(t => t.File)
            .WithMany(f => f.Tags)
            .HasForeignKey(t => t.FileId);
        
    }
}
