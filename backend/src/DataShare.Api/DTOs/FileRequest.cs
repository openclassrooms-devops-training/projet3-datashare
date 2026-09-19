namespace DataShare.Api.DTOs;

public class FileRequest
{
    public IFormFile File { get; set; } = null!;
    public string? Password { get; set; }
    public int ExpiresInDays { get; set; } = 7;
    public List<string>? Tags { get; set; }
}
