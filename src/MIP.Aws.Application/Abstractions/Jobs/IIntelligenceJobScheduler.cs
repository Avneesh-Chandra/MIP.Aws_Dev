namespace MIP.Aws.Application.Abstractions.Jobs;

/// <summary>
/// Enqueues Hangfire intelligence work without referencing Hangfire from the Application layer.
/// </summary>
public interface IIntelligenceJobScheduler
{
    void EnqueueProcessDownloadJob(Guid downloadJobId);

    void EnqueueProcessDownloadedFile(Guid downloadedFileId);

    void EnqueueArticleAiAnalysis(Guid extractedArticleId);

    void EnqueueRetryFailedAi();
}
