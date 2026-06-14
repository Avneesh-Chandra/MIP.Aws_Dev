namespace MIP.Aws.Blazor.Services;

/// <summary>
/// Formats stored instants using the viewer browser IANA time zone (resolved once per circuit).
/// </summary>
public interface IUserTimeZone
{
    bool IsResolved { get; }
    string? IanaTimeZoneId { get; }
    string DisplayHint { get; }
    event Action? Changed;
    void SetFromBrowserIana(string? ianaTimeZoneId);
    string Format(DateTimeOffset? value, string format = "yyyy-MM-dd HH:mm");
    string Format(DateTimeOffset value, string format);
}
