using AttendanceControlSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace AttendanceControlSystem.Data;

public static class DbSeeder
{
    private static readonly string[] RequiredTables =
    {
        "Users", "People", "Units", "AttendanceRecords", "AuditLogs"
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("AttendanceControlSystem.Data.DbSeeder");

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Automatic account seeding is disabled outside the Development environment.");
        }

        if (!configuration.GetValue("SeedAccounts:CreateDevelopmentAccounts", true))
        {
            throw new InvalidOperationException(
                "Development account seeding is disabled and no user account exists.");
        }

        await EnsureSchemaAsync(ctx);

        if (await ctx.Users.AnyAsync())
        {
            return;
        }

        var adminPassword = configuration["SeedAccounts:AdminPassword"] ?? "admin123";
        var operatorPassword = configuration["SeedAccounts:OperatorPassword"] ?? "operator123";
        var hasher = new PasswordHasher<User>();

        var admin = new User
        {
            Username = "admin",
            PasswordHash = hasher.HashPassword(null!, adminPassword),
            Role = UserRole.Admin,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };

        var operatorUser = new User
        {
            Username = "operator",
            PasswordHash = hasher.HashPassword(null!, operatorPassword),
            Role = UserRole.Operator,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };

        await using var transaction = await ctx.Database.BeginTransactionAsync();
        ctx.Users.AddRange(admin, operatorUser);
        await ctx.SaveChangesAsync();
        await transaction.CommitAsync();
        logger.LogWarning(
            "Development accounts were created. Change both passwords before any non-development deployment.");
    }

    private static async Task EnsureSchemaAsync(AttendanceDbContext ctx)
    {
        await ctx.Database.OpenConnectionAsync();
        try
        {
            var existingTableCount = await CountTablesAsync(ctx);
            if (existingTableCount == 0)
            {
                await ctx.Database.EnsureCreatedAsync();
            }
            else
            {
                await EnsureRequiredTablesAndColumnsAsync(ctx);
            }

            await CreateIndexesAsync(ctx);
        }
        finally
        {
            await ctx.Database.CloseConnectionAsync();
        }
    }

    private static async Task<int> CountTablesAsync(AttendanceDbContext ctx)
    {
        await using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Users', 'People', 'Units', 'AttendanceRecords', 'AuditLogs');";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task EnsureRequiredTablesAndColumnsAsync(AttendanceDbContext ctx)
    {
        foreach (var table in RequiredTables)
        {
            var columns = await GetColumnsAsync(ctx, table);
            var missing = GetRequiredColumns(table).Except(columns, StringComparer.OrdinalIgnoreCase).ToArray();
            if (missing.Length == 0)
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Existing database schema is missing columns in {table}: {string.Join(", ", missing)}. " +
                "The database was not modified. Back up the database and apply the documented schema migration before starting the application.");
        }
    }

    private static async Task<HashSet<string>> GetColumnsAsync(AttendanceDbContext ctx, string table)
    {
        await using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static string[] GetRequiredColumns(string table) => table switch
    {
        "Users" => new[]
        {
            "Id", "Username", "PasswordHash", "Role", "IsActive", "SecurityStamp",
            "CreatedAtUtc", "LastLoginAtUtc", "IsLocked", "LastActivityAt", "CreatedAt", "LockedAt"
        },
        "People" => new[]
        {
            "Id", "NationalId", "FirstName", "LastName", "Phone", "Organization",
            "IsActive", "UnitId", "CreatedAtUtc", "UpdatedAtUtc"
        },
        "Units" => new[]
        {
            "Id", "Name", "Description", "IsActive", "CreatedAtUtc"
        },
        "AttendanceRecords" => new[]
        {
            "Id", "PersonId", "EntryAtUtc", "ExitAtUtc", "EntryDescription",
            "ExitDescription", "RegisteredByUserId", "UnitId", "Note"
        },
        "AuditLogs" => new[]
        {
            "Id", "UserId", "Username", "AtUtc", "Action", "Details", "IpAddress", "CreatedAtUtc"
        },
        _ => Array.Empty<string>()
    };

    private static async Task CreateIndexesAsync(AttendanceDbContext ctx)
    {
        const string userIndex = "CREATE UNIQUE INDEX IF NOT EXISTS UX_Users_Username ON Users (Username);";
        const string personIndex = "CREATE UNIQUE INDEX IF NOT EXISTS UX_People_NationalId ON People (NationalId);";
        const string openAttendanceIndex =
            "CREATE UNIQUE INDEX IF NOT EXISTS UX_AttendanceRecords_OpenPerson ON AttendanceRecords (PersonId) WHERE ExitAtUtc IS NULL;";

        try
        {
            await ctx.Database.ExecuteSqlRawAsync(userIndex);
            await ctx.Database.ExecuteSqlRawAsync(personIndex);
            await ctx.Database.ExecuteSqlRawAsync(openAttendanceIndex);
        }
        catch (DbException ex)
        {
            throw new InvalidOperationException(
                "The existing database contains duplicate users, national IDs, or open attendance records. No data was deleted. Resolve the duplicates before creating the uniqueness indexes.",
                ex);
        }
    }
}
