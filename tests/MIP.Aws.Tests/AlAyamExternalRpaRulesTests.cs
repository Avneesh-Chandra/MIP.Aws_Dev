using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Infrastructure.News.PdfEdition;

namespace MIP.Aws.Tests;

public sealed class AlAyamExternalRpaRulesTests
{
    [Fact]
    public void ShouldSkipPublisherFetch_requires_flag_and_alayam_source()
    {
        var options = new AlAyamOutboundClientOptions { UseExternalRpa = true };
        var alAyam = new NewsSource
        {
            Name = AlAyamPublicPdfBaseline.SourceName,
            ConnectorKey = AlAyamPublicPdfBaseline.ConnectorKey
        };
        var other = new NewsSource { Name = "KSA - Al Sharq Al Awsat", ConnectorKey = "news.aawsat" };

        Assert.True(AlAyamExternalRpaRules.ShouldSkipPublisherFetch(options, alAyam));
        Assert.False(AlAyamExternalRpaRules.ShouldSkipPublisherFetch(options, other));

        options.UseExternalRpa = false;
        Assert.False(AlAyamExternalRpaRules.ShouldSkipPublisherFetch(options, alAyam));
    }

    [Fact]
    public void ApiKeysMatch_rejects_empty_or_mismatched_keys()
    {
        Assert.False(AlAyamExternalRpaRules.ApiKeysMatch(null, "x"));
        Assert.False(AlAyamExternalRpaRules.ApiKeysMatch("secret", null));
        Assert.False(AlAyamExternalRpaRules.ApiKeysMatch("secret", "other"));
        Assert.True(AlAyamExternalRpaRules.ApiKeysMatch("secret", "secret"));
    }

    [Fact]
    public void IsAllowedSource_allows_any_when_unconfigured()
    {
        var id = Guid.NewGuid();
        Assert.True(AlAyamExternalRpaRules.IsAllowedSource(id, null));
        Assert.True(AlAyamExternalRpaRules.IsAllowedSource(id, Guid.Empty));
        Assert.True(AlAyamExternalRpaRules.IsAllowedSource(id, id));
        Assert.False(AlAyamExternalRpaRules.IsAllowedSource(id, Guid.NewGuid()));
    }

    [Fact]
    public void Waiting_for_rpa_is_a_transient_alayam_failure()
    {
        Assert.True(AlAyamPublisherTiming.IsTransientPublisherFailure(AlAyamExternalRpaRules.WaitingForUploadMessage));
    }
}
