using MudBlazor;

namespace MIP.Aws.Blazor.Theme;

/// <summary>
/// Default MudBlazor theme for GFH Media Intelligence. Pulls colors from the existing executive
/// report branding so the operations UI stays visually consistent with the printed reports.
/// </summary>
public static class GfhTheme
{
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0A2342",
            Secondary = "#C5A572",
            Tertiary = "#1D3557",
            AppbarBackground = "#0A2342",
            AppbarText = "#FFFFFF",
            DrawerBackground = "#0F2C4F",
            DrawerText = "#E7ECF3",
            DrawerIcon = "#E7ECF3",
            Background = "#F4F6FA",
            Surface = "#FFFFFF",
            Success = "#1E7F3E",
            Warning = "#C58A2B",
            Error = "#A0322D",
            Info = "#2E5AAC",
            TextPrimary = "#0F1A2D",
            TextSecondary = "#3D4A60",
            ActionDefault = "#3D4A60",
            DarkLighten = "#162E55",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#C5A572",
            Secondary = "#5B8FF9",
            Tertiary = "#1D3557",
            AppbarBackground = "#091428",
            AppbarText = "#F4F6FA",
            DrawerBackground = "#091428",
            DrawerText = "#E7ECF3",
            DrawerIcon = "#E7ECF3",
            Background = "#0B1424",
            Surface = "#11243F",
            Success = "#3BB66E",
            Warning = "#E1B454",
            Error = "#E07A75",
            Info = "#7FA8F0",
            TextPrimary = "#F4F6FA",
            TextSecondary = "#B5C0D2",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Inter", "Segoe UI", "Helvetica Neue", "Arial", "sans-serif" },
                FontSize = "0.9rem",
            },
            H1 = new H1Typography { FontFamily = new[] { "Inter" }, FontSize = "2.0rem", FontWeight = "600" },
            H2 = new H2Typography { FontFamily = new[] { "Inter" }, FontSize = "1.6rem", FontWeight = "600" },
            H3 = new H3Typography { FontFamily = new[] { "Inter" }, FontSize = "1.3rem", FontWeight = "600" },
            H4 = new H4Typography { FontFamily = new[] { "Inter" }, FontSize = "1.15rem", FontWeight = "600" },
            H5 = new H5Typography { FontFamily = new[] { "Inter" }, FontSize = "1.05rem", FontWeight = "600" },
            H6 = new H6Typography { FontFamily = new[] { "Inter" }, FontSize = "0.95rem", FontWeight = "600" },
            Button = new ButtonTypography { TextTransform = "none", FontWeight = "600" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            AppbarHeight = "48px",
        },
    };
}
