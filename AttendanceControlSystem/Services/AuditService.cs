using System.Globalization;
using System.Reflection;
using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;

namespace AttendanceControlSystem.Services;

public sealed class AuditService
{
    private readonly AttendanceDbContext _dbContext;

    public AuditService(AttendanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(
        string action,
        string? entityName = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            action = "Unknown";
        }

        var auditLog = new AuditLog();

        SetProperty(
            auditLog,
            new[] { "Action", "Operation", "EventType" },
            action);

        SetProperty(
            auditLog,
            new[] { "EntityName", "EntityType", "TableName" },
            entityName);

        SetProperty(
            auditLog,
            new[] { "Details", "Description", "Message" },
            details);

        SetDateProperty(
            auditLog,
            new[] { "CreatedAt", "Timestamp", "Date" },
            DateTime.UtcNow);

        _dbContext.AuditLogs.Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task RecordAsync(
        string action,
        string? entityName = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        return LogAsync(
            action,
            entityName,
            details,
            cancellationToken);
    }

    private static void SetProperty(
        object target,
        string[] propertyNames,
        object? value)
    {
        if (value is null)
        {
            return;
        }

        foreach (var propertyName in propertyNames)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.IgnoreCase);

            if (property is null || !property.CanWrite)
            {
                continue;
            }

            try
            {
                var targetType = Nullable.GetUnderlyingType(
                    property.PropertyType)
                    ?? property.PropertyType;

                object convertedValue;

                if (targetType == typeof(string))
                {
                    convertedValue = Convert.ToString(
                        value,
                        CultureInfo.InvariantCulture) ?? string.Empty;
                }
                else if (targetType.IsEnum)
                {
                    convertedValue = Enum.Parse(
                        targetType,
                        value.ToString()!,
                        ignoreCase: true);
                }
                else
                {
                    convertedValue = Convert.ChangeType(
                        value,
                        targetType,
                        CultureInfo.InvariantCulture);
                }

                property.SetValue(target, convertedValue);
                return;
            }
            catch
            {
                // اگر property با نوع مقدار سازگار نبود،
                // property بعدی بررسی می‌شود.
            }
        }
    }

    private static void SetDateProperty(
        object target,
        string[] propertyNames,
        DateTime value)
    {
        foreach (var propertyName in propertyNames)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.IgnoreCase);

            if (property is null || !property.CanWrite)
            {
                continue;
            }

            try
            {
                if (property.PropertyType == typeof(DateTime))
                {
                    property.SetValue(target, value);
                    return;
                }

                if (property.PropertyType == typeof(DateTime?))
                {
                    property.SetValue(target, (DateTime?)value);
                    return;
                }
            }
            catch
            {
                // در صورت ناسازگاری، property بعدی بررسی می‌شود.
            }
        }
    }
}