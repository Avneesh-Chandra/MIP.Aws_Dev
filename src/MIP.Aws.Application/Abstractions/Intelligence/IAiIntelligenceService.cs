using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Application.Abstractions.Intelligence;

/// <summary>
/// Bedrock-backed enrichment for a single extracted article (licensed text only).
/// </summary>
public interface IAiIntelligenceService
{
    Task AnalyzeArticleAsync(ExtractedArticle article, CancellationToken cancellationToken);
}
