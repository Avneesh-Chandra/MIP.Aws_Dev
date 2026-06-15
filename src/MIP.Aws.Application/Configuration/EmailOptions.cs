namespace MIP.Aws.Application.Configuration;

/// <summary>Email delivery provider selection.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Mock | Smtp | AwsSes</summary>
    public string Provider { get; set; } = "Mock";

    public string FromEmail { get; set; } = string.Empty;

    public string? FromDisplayName { get; set; }
}
