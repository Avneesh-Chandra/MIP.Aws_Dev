namespace MIP.Aws.Application.Configuration;

/// <summary>Top-level AI provider selection for MIP.Aws.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Mock | AwsBedrock | OpenAiCompatible</summary>
    public string Provider { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>When true, forces the Mock provider regardless of <see cref="Provider"/>.</summary>
    public bool MockMode { get; set; }
}
