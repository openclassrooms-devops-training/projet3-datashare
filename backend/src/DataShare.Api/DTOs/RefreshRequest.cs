using System.ComponentModel.DataAnnotations;

namespace DataShare.Api.DTOs;

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = null!;
}
