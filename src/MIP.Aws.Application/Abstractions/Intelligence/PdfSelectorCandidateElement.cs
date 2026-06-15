namespace MIP.Aws.Application.Abstractions.Intelligence;

/// <summary>
/// Sanitized DOM candidate sent to the AI provider for selector suggestion (no secrets).
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
