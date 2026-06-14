using MIP.Aws.Application.News;

namespace MIP.Aws.Infrastructure.News;

internal static class HeadlineQuality
{
    public static bool IsReadableHeadline(string? headline, string? sourceName = null) =>
        ArticleContentQuality.IsReadableHeadline(headline, sourceName);

    public static bool IsReadableSnippet(string? text) =>
        ArticleContentQuality.IsReadableSnippet(text);

    public static string NormalizeSnippet(string text, int maxLen = 320) =>
        ArticleContentQuality.NormalizeSnippet(text, maxLen);
}
