using System.ComponentModel.DataAnnotations;

namespace AttendanceControlSystem.Models;

public enum UserRole
{
    Admin = 1,
    Operator = 2
}

public sealed class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Operator;

    public bool IsActive { get; set; } = true;

    public string SecurityStamp { get; set; } =
        Guid.NewGuid().ToString("N");

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime? LastLoginAtUtc { get; set; }

    public bool IsLocked { get; set; } = false;

    public DateTime? LastActivityAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LockedAt { get; set; }

}