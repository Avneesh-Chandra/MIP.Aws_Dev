using MIP.Aws.Application.Abstractions.News;

namespace MIP.Aws.Tests;

public sealed class SourcePageEditionDateCheckTests
{
    [Fact]
    public void Unparseable_does_not_block_download()
    {
        var expected = new DateOnly(2026, 6, 24);
        var check = SourcePageEditionDateCheck.Unparseable(expected, "header snippet");

        Assert.True(check.Passed);
        Assert.False(check.BlocksDownload);
    }

    [Fact]
    public void Mismatch_blocks_download()
    {
        var expected = new DateOnly(2026, 6, 24);
        var parsed = new DateOnly(2026, 6, 23);
        var check = SourcePageEditionDateCheck.Mismatch(expected, parsed, "snippet");

        Assert.False(check.Passed);
        Assert.True(check.BlocksDownload);
    }
}
