namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Local filesystem storage roots (relative to host content root unless <see cref="UseAbsoluteRoot"/> is true).
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>When true, <see cref="RootPath"/> is treated as a full directory path.</summary>
    public bool UseAbsoluteRoot { get; set; }

    /// <summary>Base directory for all stored artifacts.</summary>
    public string RootPath { get; set; } = "App_Data/storage";

    public string NewspapersRelativePath { get; set; } = "newspapers";

    public string HtmlRelativePath { get; set; } = "html";

    public string PdfRelativePath { get; set; } = "pdfs";

    public string RawRelativePath { get; set; } = "raw";

    public string OcrRelativePath { get; set; } = "ocr";

    public string ArticlesRelativePath { get; set; } = "articles";

    public string AiRelativePath { get; set; } = "ai";

    public string ProcessedRelativePath { get; set; } = "processed";

    public string ReportsPdfRelativePath { get; set; } = "reports/pdf";

    public string ReportsExcelRelativePath { get; set; } = "reports/excel";

    public string ReportsHtmlRelativePath { get; set; } = "reports/html";
}
