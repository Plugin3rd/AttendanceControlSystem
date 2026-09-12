using System.ComponentModel.DataAnnotations;

namespace AttendanceControlSystem.Models;

public sealed record LoginRequest(
    string Username,
    string Password);

public sealed record LoginResponse(
    string Token,
    int UserId,
    string Username,
    string Role);

public sealed record CreatePersonRequest(
    string NationalId,
    string FirstName,
    string LastName,
    string? Phone,
    string? Organization);

public sealed record UpdatePersonRequest(
    string FirstName,
    string LastName,
    string? Phone,
    string? Organization,
    bool IsActive);

public sealed record AttendanceRequest(
    string NationalId,
    string? Description);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public sealed record CreateUserRequest(
    string Username,
    string Password,
    UserRole Role);

public sealed record UpdateUserStatusRequest(
    bool IsActive);

public sealed record CreateUnitRequest(
    string Name,
    string? Description);

public sealed record UpdateUnitRequest(
    string Name,
    string? Description,
    bool IsActive);


public class UserCreateDto
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = "User";

    public bool IsLocked { get; set; } = false;
}

public class PasswordDto
{
    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    // برای سازگاری با کدی که ممکن است از NewPassword استفاده کند
    public string NewPassword { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UnlockDto
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class PersonDto
{
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public string NationalId { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public int? UnitId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UnitDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class AttendanceEntryDto
{
    [Required]
    public string NationalId { get; set; } = string.Empty;

    public int? UnitId { get; set; }

    public string? Note { get; set; }
}

public class AttendanceExitDto
{
    [Required]
    public string NationalId { get; set; } = string.Empty;

    public string? Note { get; set; }
}

public class ImportError
{
    public int Line { get; set; }
    public string Message { get; set; } = string.Empty;

    public ImportError(int line, string message)
    {
        Line = line;
        Message = message;
    }
}

public class ImportErrorFile
{
    public int Imported { get; set; }
    public List<ImportError> Errors { get; set; } = new();
    public string? DownloadId { get; set; }

    public ImportErrorFile(int imported, List<ImportError> errors, string? downloadId)
    {
        Imported = imported;
        Errors = errors;
        DownloadId = downloadId;
    }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }

    public PagedResult(List<T> items, int page, int pageSize, int total)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        Total = total;
    }
}

