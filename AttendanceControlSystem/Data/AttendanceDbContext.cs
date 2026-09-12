using AttendanceControlSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceControlSystem.Data
{
    public sealed class AttendanceDbContext : DbContext
    {
        public AttendanceDbContext(
            DbContextOptions<AttendanceDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Person> People => Set<Person>();
        public DbSet<Unit> Units => Set<Unit>();
        public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    }
}
