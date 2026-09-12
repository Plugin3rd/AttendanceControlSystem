using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AttendanceControlSystem.Services;

public record BackupFileInfo(string Name, long Size, DateTime Created);

public sealed class BackupService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<BackupService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public (string Name, long Size, string? Error) CreateBackup()
    {
        return CreateBackupAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    public async Task<(string Name, long Size, string? Error)> CreateBackupAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var databasePath = GetDatabasePath();

        if (!File.Exists(databasePath))
        {
            return (null!, 0, "فایل پایگاه داده پیدا نشد.");
        }

        var backupDirectory = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "Backups");

        Directory.CreateDirectory(backupDirectory);

        var backupFileName =
            $"attendance-{DateTime.Now:yyyyMMdd-HHmmss-fff}.db";

        var backupPath = Path.Combine(
            backupDirectory,
            backupFileName);

        try
        {
            var sourceConnectionString =
                new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadOnly
                }.ToString();

            var destinationConnectionString =
                new SqliteConnectionStringBuilder
                {
                    DataSource = backupPath,
                    Mode = SqliteOpenMode.ReadWriteCreate
                }.ToString();

            await using var sourceConnection =
                new SqliteConnection(sourceConnectionString);

            await using var destinationConnection =
                new SqliteConnection(destinationConnectionString);

            await sourceConnection.OpenAsync(cancellationToken);
            await destinationConnection.OpenAsync(cancellationToken);

            sourceConnection.BackupDatabase(destinationConnection);

            _logger.LogInformation(
                "Database backup created at {BackupPath}",
                backupPath);

            var fileInfo = new FileInfo(backupPath);
            return (backupFileName, fileInfo.Length, null);
        }
        catch
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            return (null!, 0, "خطا در ایجاد نسخه پشتیبان.");
        }
    }

    public Task<(string Name, long Size, string? Error)> BackupAsync(
        CancellationToken cancellationToken = default)
    {
        return CreateBackupAsync(cancellationToken);
    }

    public IEnumerable<BackupFileInfo> List()
    {
        var backupDirectory = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "Backups");

        if (!Directory.Exists(backupDirectory))
        {
            return Enumerable.Empty<BackupFileInfo>();
        }

        return Directory.EnumerateFiles(backupDirectory, "*.db")
            .Select(path =>
            {
                var fileInfo = new FileInfo(path);
                return new BackupFileInfo(
                    fileInfo.Name,
                    fileInfo.Length,
                    fileInfo.CreationTimeUtc);
            })
            .OrderByDescending(b => b.Created);
    }

    public (byte[]? Content, string? FileName) Download(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (null, null);
        }

        var backupDirectory = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "Backups");

        var backupPath = Path.Combine(backupDirectory, name);

        if (!File.Exists(backupPath))
        {
            return (null, null);
        }

        return (File.ReadAllBytes(backupPath), name);
    }

    private string GetDatabasePath()
    {
        var connectionString =
            _configuration.GetConnectionString("DefaultConnection")
            ?? _configuration["ConnectionStrings:Default"]
            ?? "Data Source=App_Data/attendance.db";

        var connectionBuilder =
            new SqliteConnectionStringBuilder(connectionString);

        var dataSource = connectionBuilder.DataSource;

        if (string.IsNullOrWhiteSpace(dataSource))
        {
            dataSource = "App_Data/attendance.db";
        }

        if (dataSource.Equals(
                ":memory:",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "برای پایگاه دادهٔ حافظه‌ای امکان تهیهٔ نسخهٔ پشتیبان وجود ندارد.");
        }

        dataSource = dataSource.Trim('"');

        if (Path.IsPathRooted(dataSource))
        {
            return dataSource;
        }

        return Path.GetFullPath(
            Path.Combine(
                _environment.ContentRootPath,
                dataSource));
    }
}
