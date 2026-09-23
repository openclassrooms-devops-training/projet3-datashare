namespace DataShare.Api.DTOs;

public class FileMetadataResponse
{
    public string Filename { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool RequiresPassword { get; set; }
}
