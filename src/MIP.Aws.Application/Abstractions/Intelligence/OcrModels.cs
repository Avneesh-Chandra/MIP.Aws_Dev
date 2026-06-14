namespace MIP.Aws.Application.Abstractions.Intelligence;

public sealed record OcrPageDto(int PageNumber, string Text, double? Confidence);

public sealed record OcrDocumentResult(
    bool Success,
    string? ErrorMessage,
    IReadOnlyList<OcrPageDto> Pages,
    string? AggregateJson);
