using System.Collections.Concurrent;

namespace AttendanceControlSystem.Services;

public sealed class LoginThrottleService
{
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);
    private const int MaximumFailures = 5;
    private readonly ConcurrentDictionary<string, AttemptState> _attempts = new();

    public bool CanAttempt(string key, out TimeSpan? retryAfter)
    {
        key = NormalizeKey(key);
        var now = DateTimeOffset.UtcNow;
        if (!_attempts.TryGetValue(key, out var state))
        {
            retryAfter = null;
            return true;
        }

        if (state.LockedUntil.HasValue && state.LockedUntil.Value > now)
        {
            retryAfter = state.LockedUntil.Value - now;
            return false;
        }

        _attempts.TryRemove(key, out _);
        retryAfter = null;
        return true;
    }

    public void RecordFailure(string key)
    {
        key = NormalizeKey(key);
        var now = DateTimeOffset.UtcNow;
        _attempts.AddOrUpdate(
            key,
            _ => new AttemptState(1, null),
            (_, current) => CreateNextState(current, now));
    }

    public void RecordSuccess(string key)
    {
        Reset(key);
    }

    public void Reset(string key)
    {
        _attempts.TryRemove(NormalizeKey(key), out _);
    }

    private static AttemptState CreateNextState(AttemptState current, DateTimeOffset now)
    {
        if (current.LockedUntil.HasValue && current.LockedUntil.Value > now)
        {
            return new AttemptState(current.FailureCount + 1, current.LockedUntil);
        }

        var failureCount = current.FailureCount + 1;
        var lockedUntil = failureCount >= MaximumFailures
            ? now.Add(LockoutWindow)
            : (DateTimeOffset?)null;
        return new AttemptState(failureCount, lockedUntil);
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? "unknown" : key.Trim();
    }

    private sealed record AttemptState(int FailureCount, DateTimeOffset? LockedUntil);
}
