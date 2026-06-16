using System.Globalization;
using MIP.Aws.Application.Configuration;

namespace MIP.Aws.Application.Time;

/// <summary>Converts UTC instants to the configured GFH display zone (default Asia/Bahrain).</summary>
public static class MipDisplayTimeZone
{
    public const string DefaultIanaId = "Asia/Bahrain";
    public const string DefaultLabel = "Bahrain";

    public static TimeZoneInfo Resolve(string? ianaId = null)
    {
        var id = string.IsNullOrWhiteSpace(ianaId) ? DefaultIanaId : ianaId.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static string Format(
        DateTimeOffset? value,
        string format = "yyyy-MM-dd HH:mm",
        string? ianaId = null)
    {
        if (value is null)
        {
            return "—";
        }

        return Format(value.Value, format, ianaId);
    }

    public static string Format(
        DateTimeOffset value,
        string format = "yyyy-MM-dd HH:mm",
        string? ianaId = null)
    {
        var tz = Resolve(ianaId);
        if (tz.Id == TimeZoneInfo.Utc.Id)
        {
            return value.ToUniversalTime().ToString(format, CultureInfo.InvariantCulture) + " UTC";
        }

        var local = TimeZoneInfo.ConvertTime(value, tz);
        return local.ToString(format, CultureInfo.InvariantCulture) + $" {ZoneSuffix(ianaId)}";
    }

    /// <summary>Formats a UTC time-of-day (HH:mm) in the display zone for scheduler labels.</summary>
    public static string FormatUtcTimeOfDay(string utcTimeHHmm, string? ianaId = null)
    {
        if (!TimeOnly.TryParse(utcTimeHHmm.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var utcTime))
        {
            return utcTimeHHmm;
        }

        var utcToday = new DateTimeOffset(DateOnly.FromDateTime(DateTime.UtcNow), utcTime, TimeSpan.Zero);
        return Format(utcToday, "HH:mm", ianaId);
    }

    public static string ZoneSuffix(string? ianaId = null)
    {
        var id = string.IsNullOrWhiteSpace(ianaId) ? DefaultIanaId : ianaId.Trim();
        return id.Equals(DefaultIanaId, StringComparison.OrdinalIgnoreCase) ? DefaultLabel : id;
    }

    public static string DisplayHint(ApplicationDisplayOptions? options = null) =>
        $"Times: {ZoneSuffix(options?.DisplayTimeZoneId)} ({options?.DisplayTimeZoneId ?? DefaultIanaId})";
}
