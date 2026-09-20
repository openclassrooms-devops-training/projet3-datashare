namespace DataShare.Api.Models;

public class File
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }              // nullable : upload anonyme US07, hors scope ici mais le MCD le prévoit
    public User? User { get; set; }
    public string OriginalFilename { get; set; } = null!;
    public string ContentType { get; set; } = null!; // detecte serveur (magic bytes), jamais celui declare par le client
    public string StoragePath { get; set; } = null!;
    public long SizeBytes { get; set; }
    public string DownloadToken { get; set; } = null!; // unique, non predictible
    public string? PasswordHash { get; set; }          // nullable
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
