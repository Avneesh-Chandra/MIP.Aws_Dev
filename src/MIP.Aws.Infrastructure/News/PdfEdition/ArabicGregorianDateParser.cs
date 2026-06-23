using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

/// <summary>Parses Gregorian edition dates from Arabic and English newspaper header text.</summary>
public static partial class ArabicGregorianDateParser
{
    [GeneratedRegex(
        @"(?<day>\d{1,2})\s+(?<month>" + @"يناير|فبراير|مارس|أبريل|ابريل|مايو|يونيو|يوليو|أغسطس|اغسطس|سبتمبر|أكتوبر|اكتوبر|نوفمبر|ديسمبر" + @")\s+(?<year>\d{4})",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ArabicGregorianRegex();

    [GeneratedRegex(
        @"(?<day>\d{1,2})\s+(?<month>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+(?<year>\d{4})",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EnglishGregorianRegex();

    public static bool TryParseFirst(string? text, out DateOnly editionDate, out string? matchedSnippet)
    {
        editionDate = default;
        matchedSnippet = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = NormalizeDigits(text);
        foreach (Match match in ArabicGregorianRegex().Matches(normalized))
        {
            if (TryBuildArabicDate(match, out editionDate))
            {
                matchedSnippet = TrimSnippet(match.Value);
                return true;
            }
        }

        foreach (Match match in EnglishGregorianRegex().Matches(normalized))
        {
            if (TryBuildEnglishDate(match, out editionDate))
            {
                matchedSnippet = TrimSnippet(match.Value);
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildArabicDate(Match match, out DateOnly editionDate)
    {
        editionDate = default;
        if (!int.TryParse(match.Groups["day"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var day)
            || !int.TryParse(match.Groups["year"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var year))
        {
            return false;
        }

        var month = match.Groups["month"].Value;
        var monthNumber = month switch
        {
            var m when m.Contains("يناير", StringComparison.Ordinal) => 1,
            var m when m.Contains("فبراير", StringComparison.Ordinal) => 2,
            var m when m.Contains("مارس", StringComparison.Ordinal) => 3,
            var m when m.Contains("أبريل", StringComparison.Ordinal) || m.Contains("ابريل", StringComparison.Ordinal) => 4,
            var m when m.Contains("مايو", StringComparison.Ordinal) => 5,
            var m when m.Contains("يونيو", StringComparison.Ordinal) => 6,
            var m when m.Contains("يوليو", StringComparison.Ordinal) => 7,
            var m when m.Contains("أغسطس", StringComparison.Ordinal) || m.Contains("اغسطس", StringComparison.Ordinal) => 8,
            var m when m.Contains("سبتمبر", StringComparison.Ordinal) => 9,
            var m when m.Contains("أكتوبر", StringComparison.Ordinal) || m.Contains("اكتوبر", StringComparison.Ordinal) => 10,
            var m when m.Contains("نوفمبر", StringComparison.Ordinal) => 11,
            var m when m.Contains("ديسمبر", StringComparison.Ordinal) => 12,
            _ => 0
        };

        if (monthNumber == 0)
        {
            return false;
        }

        try
        {
            editionDate = new DateOnly(year, monthNumber, day);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryBuildEnglishDate(Match match, out DateOnly editionDate)
    {
        editionDate = default;
        var raw = $"{match.Groups["day"].Value} {match.Groups["month"].Value} {match.Groups["year"].Value}";
        return DateOnly.TryParseExact(
            raw,
            ["d MMM yyyy", "dd MMM yyyy"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out editionDate);
    }

    private static string NormalizeDigits(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            sb.Append(c switch
            {
                '٠' => '0',
                '١' => '1',
                '٢' => '2',
                '٣' => '3',
                '٤' => '4',
                '٥' => '5',
                '٦' => '6',
                '٧' => '7',
                '٨' => '8',
                '٩' => '9',
                _ => c
            });
        }

        return sb.ToString();
    }

    private static string TrimSnippet(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= 120 ? trimmed : trimmed[..120] + "…";
    }
}
