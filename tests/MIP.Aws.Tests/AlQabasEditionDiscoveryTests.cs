using System.Text.RegularExpressions;

namespace MIP.Aws.Tests;

public sealed class AlQabasEditionDiscoveryTests
{
    [Fact]
    public void ArchivePdfRegex_picks_latest_alqabas_timestamp()
    {
        const string html = """
            "link":"https://d.alqabas.com/archive/1777486513493_old.pdf"
            "link":"https://d.alqabas.com/archive/1779047356841_latest.pdf"
            """;

        var regex = new Regex(
            @"https://d\.alqabas\.com/archive/(\d+)_[^""']+\.pdf",
            RegexOptions.IgnoreCase);

        string? best = null;
        long bestTs = 0;
        foreach (Match match in regex.Matches(html))
        {
            if (long.TryParse(match.Groups[1].Value, out var ts) && ts >= bestTs)
            {
                bestTs = ts;
                best = match.Value;
            }
        }

        Assert.Contains("1779047356841", best);
    }
}
