namespace MIP.Aws.Application.Features.NewsSources;

/// <summary>
/// Result of POST .../test-login (no edition download).
/// </summary>
public sealed record NewsPortalLoginTestResultDto(
    bool Success,
    string Message,
    string? FailureCode,
    string? ScreenshotRelativePath,
    string? HtmlSnapshotRelativePath);

/// <summary>
/// Result of POST .../test-logout (releases PressReader concurrent session slots).
/// </summary>
public sealed record NewsPortalLogoutTestResultDto(
    bool Success,
    string Message,
    string? FailureCode,
    string? ScreenshotRelativePath,
    string? HtmlSnapshotRelativePath,
    bool HadActiveSession);
