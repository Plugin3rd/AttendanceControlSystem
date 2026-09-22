using System.ComponentModel.DataAnnotations;

namespace AttendanceControlSystem.Models;

public sealed class Person
{
    public int Id { get; set; }

    [Required]
    [MaxLength(10)]
    public string NationalId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Organization { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int? UnitId { get; set; }

    public Unit? Unit { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
