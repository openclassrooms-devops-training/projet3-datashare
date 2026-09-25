namespace DataShare.Api.DTOs;

public class UserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
