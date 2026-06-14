using MIP.Aws.Application.Features.NewsSources;

namespace MIP.Aws.Blazor.Components.Pages.Admin;

/// <summary>
/// Dar Al Khaleej branded PressReader editions managed from PDF management (not news-sources actions).
/// </summary>
public static class DarAlKhaleejPressReaderUi
{
    public const string Host = "daralkhaleej.pressreader.com";

    public static readonly string[] EditionPaths =
    [
        "al-khaleej",
        "al-khaleej-9aj7",
        "alkhaleej-economy"
    ];

    public const int PressReaderEditorTabIndex = 4;

    public static bool IsLicensedPortal(NewsSourceListItemDto source) =>
        PdfManagementSourceRules.IsLicensedDarAlKhaleejPressReader(source);
}
