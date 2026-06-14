using System.Globalization;

namespace MIP.Aws.Blazor.Services;

public sealed class UserTimeZoneService : IUserTimeZone
{
    TimeZoneInfo? _tz;
    string? _iana;
    bool _resolved;

    public bool IsResolved => _resolved;
    public string? IanaTimeZoneId => _iana;

    public string DisplayHint
    {
        get
        {
            if (!_resolved)
                return "Resolving your time zone…";
            if (!string.IsNullOrWhiteSpace(_iana))
                return $"Times: {_iana}";
            return "Times: UTC (browser zone unavailable)";
        }
    }

    public event Action? Changed;

    public void SetFromBrowserIana(string? ianaTimeZoneId)
    {
        if (_resolved)
            return;

        _iana = string.IsNullOrWhiteSpace(ianaTimeZoneId) ? null : ianaTimeZoneId.Trim();
        if (_iana is null)
        {
            _tz = TimeZoneInfo.Utc;
        }
        else
        {
            try
            {
                _tz = TimeZoneInfo.FindSystemTimeZoneById(_iana);
            }
            catch (TimeZoneNotFoundException)
            {
                _tz = TimeZoneInfo.Utc;
                _iana = null;
            }
            catch (InvalidTimeZoneException)
            {
                _tz = TimeZoneInfo.Utc;
                _iana = null;
            }
        }

        _resolved = true;
        Changed?.Invoke();
    }

    public string Format(DateTimeOffset? value, string format = "yyyy-MM-dd HH:mm")
    {
        if (value is null)
            return "—";
        return Format(value.Value, format);
    }

    public string Format(DateTimeOffset value, string format)
    {
        if (!_resolved || _tz is null)
            return value.ToUniversalTime().ToString(format, CultureInfo.InvariantCulture) + " UTC";

        var local = TimeZoneInfo.ConvertTime(value, _tz);
        var s = local.ToString(format, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(_iana))
            return s + " UTC";
        return s;
    }
}
