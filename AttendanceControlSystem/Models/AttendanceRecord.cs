using System.ComponentModel.DataAnnotations;

namespace AttendanceControlSystem.Models;

public sealed class AttendanceRecord
{
    public int Id { get; set; }

    public int PersonId { get; set; }

    public Person Person { get; set; } = null!;

    [Required]
    public DateTime EntryAtUtc { get; set; }

    public DateTime? ExitAtUtc { get; set; }

    [Required]
    [MaxLength(500)]
    public string EntryDescription { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string ExitDescription { get; set; } = string.Empty;

    public int? RegisteredByUserId { get; set; }

    public User? RegisteredByUser { get; set; }

    public int? UnitId { get; set; }

    public Unit? Unit { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Note { get; set; } = string.Empty;
}
