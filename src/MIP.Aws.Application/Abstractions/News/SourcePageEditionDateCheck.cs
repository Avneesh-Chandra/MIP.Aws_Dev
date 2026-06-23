namespace MIP.Aws.Application.Abstractions.News;

public sealed record SourcePageEditionDateCheck(
    bool WasChecked,
    bool Passed,
    DateOnly ExpectedEditionDate,
    DateOnly? ParsedEditionDate,
    string? ExtractedSnippet,
    string? FailureMessage)
{
    public bool BlocksDownload => WasChecked && !Passed;

    public static SourcePageEditionDateCheck Skipped(DateOnly expected) =>
        new(false, true, expected, null, null, null);

    public static SourcePageEditionDateCheck Matched(
        DateOnly expected,
        DateOnly parsed,
        string? snippet) =>
        new(true, true, expected, parsed, snippet, null);

    public static SourcePageEditionDateCheck Mismatch(
        DateOnly expected,
        DateOnly parsed,
        string? snippet) =>
        new(
            true,
            false,
            expected,
            parsed,
            snippet,
            $"Source page edition date is {parsed:yyyy-MM-dd}, but today's expected edition is {expected:yyyy-MM-dd}.");

    public static SourcePageEditionDateCheck Unparseable(DateOnly expected, string? snippet) =>
        new(
            true,
            false,
            expected,
            null,
            snippet,
            "Could not read the edition date from the source page header to verify today's newspaper.");
}
