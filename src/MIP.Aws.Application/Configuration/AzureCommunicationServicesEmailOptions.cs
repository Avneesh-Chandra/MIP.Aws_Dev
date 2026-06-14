namespace MIP.Aws.Application.Configuration;

public sealed class AzureCommunicationServicesEmailOptions
{
    public const string SectionName = "AzureCommunicationServicesEmail";

    public bool Enabled { get; set; }

    public string? ConnectionString { get; set; }

    public string? SenderAddress { get; set; }

    public string SenderDisplayName { get; set; } = "GFH Media Intelligence";

    public bool WaitForCompletion { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 60;

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(ConnectionString)
        && !string.IsNullOrWhiteSpace(SenderAddress);
}
