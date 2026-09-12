namespace AttendanceControlSystem.Services;

public static class NationalIdValidator
{
    /// <summary>اعتبارسنجی کد ملی ایران (الگوریتم رقم کنترلی)</summary>
    public static bool IsValid(string? nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId)) return false;
        var id = nationalId.Trim();
        if (id.Length != 10 || !id.All(char.IsDigit)) return false;
        if (id.Distinct().Count() == 1) return false; // 0000000000 و مشابه
        var digits = id.Select(c => c - '0').ToArray();
        int sum = 0;
        for (int i = 0; i < 9; i++) sum += digits[i] * (10 - i);
        var rem = sum % 11;
        int check = rem < 2 ? rem : 11 - rem;
        return check == digits[9];
    }

    public static string Normalize(string? nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId))
            return string.Empty;

        return new string(nationalId.Trim().Where(char.IsDigit).ToArray());
    }
}