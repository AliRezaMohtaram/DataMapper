using System.Text.Json;
using System.Text.RegularExpressions;

namespace Borc.DataMapper.Application.Common.Validation;

public static class ValidationRules
{
    public static bool IsValidJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool IsValidRegex(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return true;

        try
        {
            _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(250));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}