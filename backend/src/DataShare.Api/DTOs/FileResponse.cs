namespace DataShare.Api.DTOs;

public class FileResponse
{
    public Guid Id { get; set; }
    public string Filename { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public string DownloadUrl { get; set; } = null!;
    public bool HasPassword { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = null!;
    public List<string>? Tags { get; set; } = new List<string>();
}


