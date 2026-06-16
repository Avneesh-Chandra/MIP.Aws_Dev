using System.Globalization;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Time;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Blazor.Services;

public sealed class UserTimeZoneService(IOptions<ApplicationDisplayOptions> displayOptions) : IUserTimeZone
{
    readonly ApplicationDisplayOptions _display = displayOptions.Value;
    TimeZoneInfo? _tz;
    string? _iana;
    bool _resolved;

    public bool IsResolved => _resolved;
    public string? IanaTimeZoneId => _iana;

    public string DisplayHint =>
        _resolved
            ? MipDisplayTimeZone.DisplayHint(_display)
            : "Resolving time zone…";

    public event Action? Changed;

    public void SetFromBrowserIana(string? ianaTimeZoneId)
    {
        if (_resolved)
        {
            return;
        }

        _ = ianaTimeZoneId;
        _iana = string.IsNullOrWhiteSpace(_display.DisplayTimeZoneId)
            ? MipDisplayTimeZone.DefaultIanaId
            : _display.DisplayTimeZoneId.Trim();
        _tz = MipDisplayTimeZone.Resolve(_iana);
        _resolved = true;
        Changed?.Invoke();
    }

    public string Format(DateTimeOffset? value, string format = "yyyy-MM-dd HH:mm")
    {
        if (value is null)
        {
            return "—";
        }

        return Format(value.Value, format);
    }

    public string Format(DateTimeOffset value, string format)
    {
        if (!_resolved || _tz is null)
        {
            return MipDisplayTimeZone.Format(value, format, _display.DisplayTimeZoneId);
        }

        var local = TimeZoneInfo.ConvertTime(value, _tz);
        var s = local.ToString(format, CultureInfo.InvariantCulture);
        return s + $" {MipDisplayTimeZone.ZoneSuffix(_iana)}";
    }
}
