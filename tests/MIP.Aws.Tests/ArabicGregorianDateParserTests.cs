using MIP.Aws.Infrastructure.News.PdfEdition;

namespace MIP.Aws.Tests;

public sealed class ArabicGregorianDateParserTests
{
    [Theory]
    [InlineData("العدد : ١٧٦٢٣ - الثلاثاء ٢٣ يونيو ٢٠٢٦ م، الموافق ٠٨ محرّم ١٤٤٨هـ", "2026-06-23")]
    [InlineData("الثلاثاء,  23 يونيو 2026 -  08 مُحرَّم 1448 هـ", "2026-06-23")]
    [InlineData("العدد 13590 الثلاثاء 23 يونيو 2026 الموافق 8 محرم 1448", "2026-06-23")]
    [InlineData("Al Khaleej | 23 Jun 2026", "2026-06-23")]
    public void TryParseFirst_parses_gregorian_dates_from_source_headers(string text, string expected)
    {
        Assert.True(ArabicGregorianDateParser.TryParseFirst(text, out var date, out var snippet));
        Assert.Equal(DateOnly.Parse(expected), date);
        Assert.False(string.IsNullOrWhiteSpace(snippet));
    }

    [Fact]
    public void TryParseFirst_returns_false_for_text_without_gregorian_date()
    {
        Assert.False(ArabicGregorianDateParser.TryParseFirst("Latest headlines only", out _, out _));
    }
}
