using MIP.Aws.Application.Abstractions.Jobs;

namespace MIP.Aws.Infrastructure.Jobs;

/// <summary>No-op intelligence pipeline for the AWS lite stack (OCR/AI excluded).</summary>
public sealed class NoopIntelligenceJobScheduler : IIntelligenceJobScheduler
{
    public void EnqueueProcessDownloadJob(Guid downloadJobId)
    {
    }

    public void EnqueueProcessDownloadedFile(Guid downloadedFileId)
    {
    }

    public void EnqueueArticleAiAnalysis(Guid extractedArticleId)
    {
    }

    public void EnqueueRetryFailedAi()
    {
    }
}
