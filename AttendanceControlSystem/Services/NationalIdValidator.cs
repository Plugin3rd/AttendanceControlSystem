namespace AttendanceControlSystem.Services;

public static class NationalIdValidator
{
    public static bool IsValid(string? nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId) || nationalId.Length != 10)
        {
            return false;
        }

        var digits = new int[nationalId.Length];
        for (var index = 0; index < nationalId.Length; index++)
        {
            if (!TryNormalizeDigit(nationalId[index], out var digit))
            {
                return false;
            }

            digits[index] = digit;
        }

        if (digits.Distinct().Count() == 1)
        {
            return false;
        }

        var sum = 0;
        for (var index = 0; index < 9; index++)
        {
            sum += digits[index] * (10 - index);
        }

        var remainder = sum % 11;
        var checkDigit = remainder < 2 ? remainder : 11 - remainder;
        return checkDigit == digits[9];
    }

    public static string Normalize(string? nationalId)
    {
        if (nationalId is null)
        {
            return string.Empty;
        }

        return new string(nationalId.Select(NormalizeDigit).ToArray());
    }

    private static char NormalizeDigit(char character)
    {
        return TryNormalizeDigit(character, out var digit) ? digit : character;
    }

    private static bool TryNormalizeDigit(char character, out char digit)
    {
        if (character is >= '0' and <= '9')
        {
            digit = character;
            return true;
        }

        digit = character switch
        {
            '\u06F0' => '0',
            '\u06F1' => '1',
            '\u06F2' => '2',
            '\u06F3' => '3',
            '\u06F4' => '4',
            '\u06F5' => '5',
            '\u06F6' => '6',
            '\u06F7' => '7',
            '\u06F8' => '8',
            '\u06F9' => '9',
            '\u0660' => '0',
            '\u0661' => '1',
            '\u0662' => '2',
            '\u0663' => '3',
            '\u0664' => '4',
            '\u0665' => '5',
            '\u0666' => '6',
            '\u0667' => '7',
            '\u0668' => '8',
            '\u0669' => '9',
            _ => '\0'
        };

        return digit != '\0';
    }
}
