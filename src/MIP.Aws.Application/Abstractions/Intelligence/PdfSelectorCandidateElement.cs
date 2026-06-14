namespace MIP.Aws.Application.Abstractions.Intelligence;

/// <summary>
/// Sanitized DOM candidate sent to Azure OpenAI for selector suggestion (no secrets).
/// </summary>
public sealed record PdfSelectorCandidateElement(
    string Tag,
    string? Text,
    string? Href,
    string? AriaLabel,
    string? Title,
    string? Alt,
    string? Class,
    string? Id,
    string? Role);
