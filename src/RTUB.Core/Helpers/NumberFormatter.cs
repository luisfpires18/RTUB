namespace RTUB.Core.Helpers;

/// <summary>
/// Formats numbers with K/M/B/T abbreviations for compact display.
/// Values below 1,000 are returned without abbreviation.
/// </summary>
public static class NumberFormatter
{
    /// <summary>
    /// Formats an integer with compact abbreviation (1K, 1.5M, etc.)
    /// </summary>
    public static string Compact(int value)
    {
        return value < 0 ? "-" + FormatPositive((long)(-value)) : FormatPositive(value);
    }

    /// <summary>
    /// Formats a long with compact abbreviation (1K, 1.5M, etc.)
    /// </summary>
    public static string Compact(long value)
    {
        if (value == long.MinValue) return "-" + FormatPositive(long.MaxValue);
        return value < 0 ? "-" + FormatPositive(-value) : FormatPositive(value);
    }

    /// <summary>
    /// Formats a decimal with compact abbreviation (1K, 1.5M, etc.)
    /// </summary>
    public static string Compact(decimal value)
    {
        if (value < 0) return "-" + FormatPositiveDecimal(-value);
        return FormatPositiveDecimal(value);
    }

    /// <summary>
    /// Formats a double with compact abbreviation (1K, 1.5M, etc.)
    /// </summary>
    public static string Compact(double value)
    {
        return Compact((decimal)value);
    }

    /// <summary>
    /// Formats a nullable int, returning "0" if null.
    /// </summary>
    public static string Compact(int? value) => Compact(value ?? 0);

    private static string FormatPositive(long value)
    {
        if (value >= 1_000_000_000_000L)
        {
            var v = value / 1_000_000_000_000.0;
            return TrimTrailing(v) + "T";
        }
        if (value >= 1_000_000_000L)
        {
            var v = value / 1_000_000_000.0;
            return TrimTrailing(v) + "B";
        }
        if (value >= 1_000_000L)
        {
            var v = value / 1_000_000.0;
            return TrimTrailing(v) + "M";
        }
        if (value >= 1_000L)
        {
            var v = value / 1_000.0;
            return TrimTrailing(v) + "K";
        }
        return value.ToString();
    }

    private static string FormatPositiveDecimal(decimal value)
    {
        if (value >= 1_000_000_000_000m)
        {
            var v = (double)(value / 1_000_000_000_000m);
            return TrimTrailing(v) + "T";
        }
        if (value >= 1_000_000_000m)
        {
            var v = (double)(value / 1_000_000_000m);
            return TrimTrailing(v) + "B";
        }
        if (value >= 1_000_000m)
        {
            var v = (double)(value / 1_000_000m);
            return TrimTrailing(v) + "M";
        }
        if (value >= 1_000m)
        {
            var v = (double)(value / 1_000m);
            return TrimTrailing(v) + "K";
        }
        // Below 1K: show up to 2 decimal places for decimal values
        if (value == Math.Truncate(value))
            return ((long)value).ToString();
        return value.ToString("0.##");
    }

    /// <summary>
    /// Formats a number down to 1-2 significant digits after division, trimming unnecessary trailing zeros.
    /// E.g. 1500 → "1.5K", 2000 → "2K", 1230000 → "1.23M"
    /// </summary>
    private static string TrimTrailing(double value)
    {
        // Show up to 2 decimal places, trim trailing zeros
        var formatted = value.ToString("0.##");
        return formatted;
    }
}
