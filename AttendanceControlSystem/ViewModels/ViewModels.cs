using System.ComponentModel.DataAnnotations;
using AttendanceControlSystem.Models;
using AttendanceControlSystem.Services;

namespace AttendanceControlSystem_vm;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class IranianNationalIdAttribute : ValidationAttribute
{
    public bool AllowEmpty { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string nationalId || string.IsNullOrWhiteSpace(nationalId))
        {
            return AllowEmpty ? ValidationResult.Success : CreateError(validationContext);
        }

        return NationalIdValidator.IsValid(nationalId)
            ? ValidationResult.Success
            : CreateError(validationContext);
    }

    private ValidationResult CreateError(ValidationContext validationContext)
    {
        return new ValidationResult(
            ErrorMessage ?? "کد ملی معتبر نیست.",
            new[] { validationContext.MemberName ?? string.Empty });
    }
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class NotBeforePropertyAttribute : ValidationAttribute
{
    public string PropertyName { get; }

    public bool IgnoreTime { get; set; }

    public NotBeforePropertyAttribute(string propertyName)
    {
        PropertyName = propertyName;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateTime currentValue)
        {
            return ValidationResult.Success;
        }

        var otherProperty = validationContext.ObjectType.GetProperty(PropertyName);
        if (otherProperty?.GetValue(validationContext.ObjectInstance) is not DateTime otherValue)
        {
            return ValidationResult.Success;
        }

        var isInvalid = IgnoreTime
            ? currentValue.Date < otherValue.Date
            : currentValue <= otherValue;

        return isInvalid
            ? new ValidationResult(
                ErrorMessage ?? $"مقدار باید بعد از {PropertyName} باشد.",
                new[] { validationContext.MemberName ?? string.Empty })
            : ValidationResult.Success;
    }
}

public class AccountLoginViewModel
{
    [Required(ErrorMessage = "نام کاربری الزامی است.")]
    [Display(Name = "نام کاربری")]
    [StringLength(50, ErrorMessage = "نام کاربری نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور الزامی است.")]
    [Display(Name = "رمز عبور")]
    [MinLength(6, ErrorMessage = "رمز عبور باید حداقل ۶ کاراکتر باشد.")]
    [MaxLength(128, ErrorMessage = "رمز عبور نمی‌تواند بیشتر از ۱۲۸ کاراکتر باشد.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "مرا به خاطر بسپار")]
    public bool RememberMe { get; set; }

    [Display(Name = "آدرس بازگشت")]
    [StringLength(2048, ErrorMessage = "آدرس بازگشت بیش از حد طولانی است.")]
    public string? ReturnUrl { get; set; }
}

public class DashboardViewModel
{
    public int ActivePeople { get; set; }
    public int ActiveUnits { get; set; }
    public int TodayEntries { get; set; }
    public int TodayExits { get; set; }
    public int PresentCount { get; set; }
    public int ActiveUsers { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime TodayUtc { get; set; } = DateTime.UtcNow.Date;
    public List<AttendanceRecord> RecentEntries { get; set; } = new();
    public List<AttendanceRecord> PresentPeople { get; set; } = new();
    public List<User> ActiveUserList { get; set; } = new();
    public bool IsAdmin { get; set; }
}

public class PeopleIndexViewModel
{
    public List<Person> Items { get; set; } = new();

    [Display(Name = "صفحه")]
    [Range(1, int.MaxValue, ErrorMessage = "شماره صفحه باید بزرگ‌تر از صفر باشد.")]
    public int Page { get; set; } = 1;

    [Display(Name = "تعداد نمایش در هر صفحه")]
    [Range(1, 100, ErrorMessage = "تعداد نمایش در هر صفحه باید بین ۱ تا ۱۰۰ باشد.")]
    public int PageSize { get; set; } = 20;

    [Display(Name = "تعداد کل")]
    public int Total { get; set; }

    [Display(Name = "جستجو")]
    [StringLength(100, ErrorMessage = "عبارت جستجو نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string? Search { get; set; }
}

public class PersonCreateViewModel
{
    [Required(ErrorMessage = "کد ملی الزامی است.")]
    [IranianNationalId(AllowEmpty = true, ErrorMessage = "کد ملی معتبر نیست.")]
    [StringLength(10, MinimumLength = 10, ErrorMessage = "کد ملی باید دقیقاً ۱۰ رقم باشد.")]
    [Display(Name = "کد ملی")]
    public string NationalId { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام الزامی است.")]
    [Display(Name = "نام")]
    [StringLength(100, ErrorMessage = "نام نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام خانوادگی الزامی است.")]
    [Display(Name = "نام خانوادگی")]
    [StringLength(100, ErrorMessage = "نام خانوادگی نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "تلفن")]
    [StringLength(30, ErrorMessage = "تلفن نمی‌تواند بیشتر از ۳۰ کاراکتر باشد.")]
    public string? Phone { get; set; }

    [Display(Name = "سازمان")]
    [StringLength(100, ErrorMessage = "نام سازمان نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string? Organization { get; set; }

    [Display(Name = "واحد")]
    [Range(1, int.MaxValue, ErrorMessage = "واحد انتخاب‌شده معتبر نیست.")]
    public int? UnitId { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;
}

public class PersonEditViewModel
{
    [Display(Name = "شناسه")]
    [Range(1, int.MaxValue, ErrorMessage = "شناسه فرد معتبر نیست.")]
    public int Id { get; set; }

    [IranianNationalId(AllowEmpty = true, ErrorMessage = "کد ملی معتبر نیست.")]
    [StringLength(10, ErrorMessage = "کد ملی نمی‌تواند بیشتر از ۱۰ کاراکتر باشد.")]
    [Display(Name = "کد ملی")]
    public string NationalId { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام الزامی است.")]
    [Display(Name = "نام")]
    [StringLength(100, ErrorMessage = "نام نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام خانوادگی الزامی است.")]
    [Display(Name = "نام خانوادگی")]
    [StringLength(100, ErrorMessage = "نام خانوادگی نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "تلفن")]
    [StringLength(30, ErrorMessage = "تلفن نمی‌تواند بیشتر از ۳۰ کاراکتر باشد.")]
    public string? Phone { get; set; }

    [Display(Name = "سازمان")]
    [StringLength(100, ErrorMessage = "نام سازمان نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string? Organization { get; set; }

    [Display(Name = "واحد")]
    [Range(1, int.MaxValue, ErrorMessage = "واحد انتخاب‌شده معتبر نیست.")]
    public int? UnitId { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; }
}

public class UnitCreateViewModel
{
    [Required(ErrorMessage = "نام واحد الزامی است.")]
    [Display(Name = "نام واحد")]
    [StringLength(150, ErrorMessage = "نام واحد نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "توضیحات")]
    [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Description { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;
}

public class UnitEditViewModel
{
    [Display(Name = "شناسه")]
    [Range(1, int.MaxValue, ErrorMessage = "شناسه واحد معتبر نیست.")]
    public int Id { get; set; }

    [Required(ErrorMessage = "نام واحد الزامی است.")]
    [Display(Name = "نام واحد")]
    [StringLength(150, ErrorMessage = "نام واحد نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "توضیحات")]
    [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Description { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; }
}

public class AttendanceIndexViewModel
{
    public List<AttendanceRecord> Items { get; set; } = new();

    [Display(Name = "صفحه")]
    [Range(1, int.MaxValue, ErrorMessage = "شماره صفحه باید بزرگ‌تر از صفر باشد.")]
    public int Page { get; set; } = 1;

    [Display(Name = "تعداد نمایش در هر صفحه")]
    [Range(1, 100, ErrorMessage = "تعداد نمایش در هر صفحه باید بین ۱ تا ۱۰۰ باشد.")]
    public int PageSize { get; set; } = 20;

    [Display(Name = "تعداد کل")]
    public int Total { get; set; }

    [Display(Name = "شناسه فرد")]
    [Range(1, int.MaxValue, ErrorMessage = "شناسه فرد معتبر نیست.")]
    public int? PersonId { get; set; }

    [Display(Name = "کد ملی")]
    [IranianNationalId(AllowEmpty = true, ErrorMessage = "کد ملی معتبر نیست.")]
    [StringLength(10, ErrorMessage = "کد ملی نمی‌تواند بیشتر از ۱۰ کاراکتر باشد.")]
    public string? NationalId { get; set; }
}

public class AttendanceCreateViewModel
{
    [Required(ErrorMessage = "کد ملی الزامی است.")]
    [IranianNationalId(AllowEmpty = true, ErrorMessage = "کد ملی معتبر نیست.")]
    [StringLength(10, MinimumLength = 10, ErrorMessage = "کد ملی باید دقیقاً ۱۰ رقم باشد.")]
    [Display(Name = "کد ملی")]
    public string NationalId { get; set; } = string.Empty;

    [Display(Name = "واحد")]
    [Range(1, int.MaxValue, ErrorMessage = "واحد انتخاب‌شده معتبر نیست.")]
    public int? UnitId { get; set; }

    [Display(Name = "یادداشت")]
    [StringLength(1000, ErrorMessage = "یادداشت نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد.")]
    public string? Note { get; set; } = string.Empty;

    [Display(Name = "توضیحات ورود")]
    [StringLength(500, ErrorMessage = "توضیحات ورود نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? Description { get; set; } = string.Empty;
}

public class AttendanceEditViewModel
{
    [Display(Name = "شناسه")]
    [Range(1, int.MaxValue, ErrorMessage = "شناسه رکورد معتبر نیست.")]
    public int Id { get; set; }

    [Display(Name = "نام فرد")]
    public string PersonName { get; set; } = string.Empty;

    [Display(Name = "کد ملی")]
    public string NationalId { get; set; } = string.Empty;

    [Display(Name = "زمان ورود")]
    [DataType(DataType.DateTime)]
    public DateTime EntryAtUtc { get; set; }

    [Display(Name = "زمان خروج")]
    [DataType(DataType.DateTime)]
    [NotBeforeProperty(nameof(EntryAtUtc), ErrorMessage = "زمان خروج باید بعد از زمان ورود باشد.")]
    public DateTime? ExitAtUtc { get; set; }

    [Display(Name = "توضیحات خروج")]
    [StringLength(500, ErrorMessage = "توضیحات خروج نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
    public string? ExitDescription { get; set; } = string.Empty;

    [Display(Name = "یادداشت")]
    [StringLength(1000, ErrorMessage = "یادداشت نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد.")]
    public string? Note { get; set; } = string.Empty;
}

public class ReportIndexViewModel
{
    public List<AttendanceRecord> Items { get; set; } = new();

    [Display(Name = "از تاریخ")]
    [DataType(DataType.Date)]
    public DateTime? Start { get; set; }

    [Display(Name = "تا تاریخ")]
    [DataType(DataType.Date)]
    [NotBeforeProperty(nameof(Start), IgnoreTime = true, ErrorMessage = "تاریخ پایان نباید قبل از تاریخ شروع باشد.")]
    public DateTime? End { get; set; }

    [Display(Name = "واحد")]
    [Range(1, int.MaxValue, ErrorMessage = "واحد انتخاب‌شده معتبر نیست.")]
    public int? UnitId { get; set; }

    [Display(Name = "وضعیت")]
    [Range(0, 1, ErrorMessage = "وضعیت انتخاب‌شده معتبر نیست.")]
    public int? Status { get; set; }

    [Display(Name = "صفحه")]
    [Range(1, int.MaxValue, ErrorMessage = "شماره صفحه باید بزرگ‌تر از صفر باشد.")]
    public int Page { get; set; } = 1;

    [Display(Name = "تعداد نمایش در هر صفحه")]
    [Range(1, 100, ErrorMessage = "تعداد نمایش در هر صفحه باید بین ۱ تا ۱۰۰ باشد.")]
    public int PageSize { get; set; } = 20;

    [Display(Name = "تعداد کل")]
    public int Total { get; set; }

    public List<Unit> Units { get; set; } = new();
}

public class UserCreateViewModel
{
    [Required(ErrorMessage = "نام کاربری الزامی است.")]
    [Display(Name = "نام کاربری")]
    [StringLength(50, ErrorMessage = "نام کاربری نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور الزامی است.")]
    [Display(Name = "رمز عبور")]
    [MinLength(6, ErrorMessage = "رمز عبور باید حداقل ۶ کاراکتر باشد.")]
    [MaxLength(128, ErrorMessage = "رمز عبور نمی‌تواند بیشتر از ۱۲۸ کاراکتر باشد.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "نقش")]
    [EnumDataType(typeof(UserRole), ErrorMessage = "نقش انتخاب‌شده معتبر نیست.")]
    public UserRole Role { get; set; } = UserRole.Operator;

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;
}

public class UserEditViewModel
{
    [Display(Name = "شناسه")]
    [Range(1, int.MaxValue, ErrorMessage = "شناسه کاربر معتبر نیست.")]
    public int Id { get; set; }

    [Display(Name = "نام کاربری")]
    [StringLength(50, ErrorMessage = "نام کاربری نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.")]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "نقش")]
    [EnumDataType(typeof(UserRole), ErrorMessage = "نقش انتخاب‌شده معتبر نیست.")]
    public UserRole Role { get; set; } = UserRole.Operator;

    [Display(Name = "فعال")]
    public bool IsActive { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "رمز عبور فعلی الزامی است.")]
    [Display(Name = "رمز عبور فعلی")]
    [MaxLength(128, ErrorMessage = "رمز عبور فعلی نمی‌تواند بیشتر از ۱۲۸ کاراکتر باشد.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور جدید الزامی است.")]
    [Display(Name = "رمز عبور جدید")]
    [MinLength(6, ErrorMessage = "رمز عبور جدید باید حداقل ۶ کاراکتر باشد.")]
    [MaxLength(128, ErrorMessage = "رمز عبور جدید نمی‌تواند بیشتر از ۱۲۸ کاراکتر باشد.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "تکرار رمز عبور الزامی است.")]
    [Display(Name = "تکرار رمز عبور")]
    [MaxLength(128, ErrorMessage = "تکرار رمز عبور نمی‌تواند بیشتر از ۱۲۸ کاراکتر باشد.")]
    [Compare(nameof(NewPassword), ErrorMessage = "رمز عبور جدید و تکرار آن یکسان نیستند.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ErrorViewModel
{
    [Display(Name = "پیام خطا")]
    public string? Message { get; set; }

    [Display(Name = "شناسه درخواست")]
    public string? RequestId { get; set; }

    [Display(Name = "کد خطا")]
    public string? ErrorCode { get; set; }

    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);
}
