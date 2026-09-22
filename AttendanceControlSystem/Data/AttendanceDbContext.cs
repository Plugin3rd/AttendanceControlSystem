using AttendanceControlSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceControlSystem.Data;

public sealed class AttendanceDbContext : DbContext
{
    public AttendanceDbContext(DbContextOptions<AttendanceDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(user => user.Username).IsUnique();
            entity.Property(user => user.Username).IsRequired().HasMaxLength(50);
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.Property(user => user.SecurityStamp).IsRequired();
            entity.Property(user => user.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasIndex(person => person.NationalId).IsUnique();
            entity.Property(person => person.NationalId).IsRequired().HasMaxLength(10);
            entity.Property(person => person.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(person => person.LastName).IsRequired().HasMaxLength(100);
            entity.Property(person => person.Phone).HasMaxLength(30);
            entity.Property(person => person.Organization).HasMaxLength(100);
            entity.HasOne(person => person.Unit)
                .WithMany()
                .HasForeignKey(person => person.UnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Unit>(entity =>
        {
            entity.Property(unit => unit.Name).IsRequired().HasMaxLength(150);
            entity.Property(unit => unit.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasIndex(record => record.PersonId)
                .HasFilter("ExitAtUtc IS NULL")
                .IsUnique();
            entity.Property(record => record.EntryDescription).IsRequired().HasMaxLength(500);
            entity.Property(record => record.ExitDescription).IsRequired().HasMaxLength(500);
            entity.Property(record => record.Note).IsRequired().HasMaxLength(1000);
            entity.HasOne(record => record.Person)
                .WithMany()
                .HasForeignKey(record => record.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(record => record.RegisteredByUser)
                .WithMany()
                .HasForeignKey(record => record.RegisteredByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(record => record.Unit)
                .WithMany()
                .HasForeignKey(record => record.UnitId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(log => log.Action).IsRequired().HasMaxLength(100);
            entity.Property(log => log.Details).HasMaxLength(1000);
            entity.Property(log => log.IpAddress).HasMaxLength(100);
            entity.HasOne(log => log.User)
                .WithMany()
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
