namespace MIP.Aws.Application.Configuration;

public sealed class AzureDocumentIntelligenceOptions
{
    public const string SectionName = "AzureDocumentIntelligence";

    public bool Enabled { get; set; }

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model id, e.g. prebuilt-read or prebuilt-layout.</summary>
    public string ModelId { get; set; } = "prebuilt-read";
}
