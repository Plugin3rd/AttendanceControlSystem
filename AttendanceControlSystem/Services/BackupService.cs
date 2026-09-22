using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AttendanceControlSystem.Services;

public record BackupFileInfo(string Name, long Size, DateTime Created);

public sealed partial class BackupService
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
        return CreateBackupAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task<(string Name, long Size, string? Error)> CreateBackupAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? backupPath = null;

        try
        {
            var databasePath = GetDatabasePath();
            if (!File.Exists(databasePath))
            {
                return (string.Empty, 0, "فایل پایگاه داده پیدا نشد.");
            }

            var backupDirectory = GetBackupDirectory();
            Directory.CreateDirectory(backupDirectory);
            backupPath = GetAvailableBackupPath(backupDirectory);

            var sourceConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            var destinationConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = backupPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            await using var sourceConnection = new SqliteConnection(sourceConnectionString);
            await using var destinationConnection = new SqliteConnection(destinationConnectionString);
            await sourceConnection.OpenAsync(cancellationToken);
            await destinationConnection.OpenAsync(cancellationToken);
            await Task.Run(
                () => sourceConnection.BackupDatabase(destinationConnection),
                CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(backupPath);
            _logger.LogInformation("Database backup created at {BackupPath}", backupPath);
            return (Path.GetFileName(backupPath), fileInfo.Length, null);
        }
        catch (OperationCanceledException)
        {
            DeletePartialBackup(backupPath);
            throw;
        }
        catch (Exception exception)
        {
            DeletePartialBackup(backupPath);
            _logger.LogError(exception, "Database backup failed.");
            return (string.Empty, 0, "خطا در ایجاد نسخه پشتیبان.");
        }
    }

    public Task<(string Name, long Size, string? Error)> BackupAsync(
        CancellationToken cancellationToken = default)
    {
        return CreateBackupAsync(cancellationToken);
    }

    public (byte[]? Content, string? FileName) Download(string name)
    {
        return DownloadAsync(name, CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task<(byte[]? Content, string? FileName)> DownloadAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var path = GetDownloadPath(name);
        if (path is null)
        {
            return (null, null);
        }

        try
        {
            var content = await File.ReadAllBytesAsync(path, cancellationToken);
            return (content, Path.GetFileName(path));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Database backup download failed for {BackupName}.", name);
            return (null, null);
        }
    }

    public IEnumerable<BackupFileInfo> List()
    {
        var backupDirectory = GetBackupDirectory();
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
            .OrderByDescending(backup => backup.Created);
    }

    public string? GetDownloadPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || !BackupFileNameRegex().IsMatch(name))
        {
            return null;
        }

        try
        {
            var backupDirectory = Path.GetFullPath(GetBackupDirectory());
            var candidate = Path.GetFullPath(Path.Combine(backupDirectory, name));
            var rootWithSeparator = Path.TrimEndingDirectorySeparator(backupDirectory)
                + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
                && !candidate.Equals(backupDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return File.Exists(candidate) ? candidate : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Invalid backup download path was rejected.");
            return null;
        }
    }

    private string GetBackupDirectory()
    {
        return Path.Combine(_environment.ContentRootPath, "App_Data", "Backups");
    }

    private string GetDatabasePath()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            dataSource = "App_Data/attendance.db";
        }

        if (dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("تهیه نسخه پشتیبان از پایگاه داده حافظه‌ای امکان‌پذیر نیست.");
        }

        if (dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(dataSource);
                if (uri.IsFile)
                {
                    dataSource = uri.LocalPath;
                }
            }
            catch (UriFormatException)
            {
                dataSource = dataSource["file:".Length..];
            }
        }

        dataSource = dataSource.Trim('"');
        return Path.IsPathRooted(dataSource)
            ? dataSource
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, dataSource));
    }

    private static string GetAvailableBackupPath(string backupDirectory)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            var candidate = Path.Combine(backupDirectory, $"attendance-{timestamp}.db");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(backupDirectory, $"attendance-{DateTime.UtcNow:yyyyMMdd-HHmmss}-999.db");
    }

    private void DeletePartialBackup(string? backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            return;
        }

        try
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not delete partial backup {BackupPath}.", backupPath);
        }
    }

    [GeneratedRegex(@"^attendance-\d{8}-\d{6}-\d{3}\.db$", RegexOptions.CultureInvariant)]
    private static partial Regex BackupFileNameRegex();
}
