namespace DataShare.Api.Models;

public class Tag
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public File File { get; set; } = null!;
    public string Label { get; set; } = null!;
}
