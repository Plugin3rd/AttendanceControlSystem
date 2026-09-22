using System.ComponentModel.DataAnnotations;

namespace AttendanceControlSystem.Models;

public sealed class AuditLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string? Username { get; set; }

    public DateTime AtUtc { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Details { get; set; } = string.Empty;

    [MaxLength(100)]
    public string IpAddress { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
