using MIP.Aws.Infrastructure.News.PdfEdition;

namespace MIP.Aws.Tests;

public sealed class AlAyamPublisherHostsTests
{
    [Theory]
    [InlineData("https://www.alayam.com/epaper", true)]
    [InlineData("https://i.alayam.com/ayamnewsa/upload/issue/2026/1/PDF/INAF_test.pdf", true)]
    [InlineData("https://example.com/paper.pdf", false)]
    public void IsAlAyamHost_matches_alayam_domains(string url, bool expected)
    {
        Assert.Equal(expected, AlAyamPublisherHosts.IsAlAyamHost(new Uri(url)));
    }
}
