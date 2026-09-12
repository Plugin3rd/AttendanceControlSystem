using System.ComponentModel.DataAnnotations;

namespace AttendanceControlSystem.Models;

public sealed class Unit
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}
