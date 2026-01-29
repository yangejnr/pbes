using System.ComponentModel.DataAnnotations;

namespace PbesApi.Models;

public class Officer
{
    public Guid Id { get; set; }

    [Required]
    public string ServiceNumber { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Officer";
}
