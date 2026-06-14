namespace MIP.Aws.Application.Configuration;

public sealed class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAI";

    public bool Enabled { get; set; }

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string DeploymentName { get; set; } = string.Empty;

    public int MaxConcurrentArticleAnalyses { get; set; } = 4;
}
