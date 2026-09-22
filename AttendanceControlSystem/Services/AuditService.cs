using AttendanceControlSystem.Data;
using AttendanceControlSystem.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AttendanceControlSystem.Services;

public sealed class AuditService
{
    private const int ActionMaxLength = 100;
    private const int DetailsMaxLength = 1000;
    private const int UsernameMaxLength = 50;
    private const int IpAddressMaxLength = 100;

    private readonly AttendanceDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(
        AttendanceDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Add(
        string action,
        string? entityName = null,
        string? details = null,
        string? username = null,
        int? userId = null)
    {
        _dbContext.AuditLogs.Add(CreateLog(action, entityName, details, username, userId));
    }

    public async Task LogAsync(
        string action,
        string? entityName = null,
        string? details = null,
        bool saveChanges = true,
        CancellationToken cancellationToken = default,
        string? username = null,
        int? userId = null)
    {
        Add(action, entityName, details, username, userId);
        if (saveChanges)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public Task RecordAsync(
        string action,
        string? entityName = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        return LogAsync(action, entityName, details, true, cancellationToken);
    }

    private AuditLog CreateLog(
        string action,
        string? entityName,
        string? details,
        string? usernameOverride,
        int? userIdOverride)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var now = DateTime.UtcNow;
        var claimUserIdValue = httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(claimUserIdValue, out var claimUserId);
        var identityUsername = httpContext?.User.Identity?.Name;
        var remoteIpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();

        return new AuditLog
        {
            UserId = userIdOverride ?? (claimUserId > 0 ? claimUserId : null),
            Username = Normalize(
                string.IsNullOrWhiteSpace(usernameOverride) ? identityUsername : usernameOverride,
                UsernameMaxLength),
            AtUtc = now,
            Action = Normalize(action, ActionMaxLength, "Unknown"),
            Details = Normalize(details, DetailsMaxLength),
            IpAddress = Normalize(remoteIpAddress, IpAddressMaxLength),
            CreatedAtUtc = now
        };
    }

    private static string Normalize(string? value, int maxLength, string? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue ?? string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
