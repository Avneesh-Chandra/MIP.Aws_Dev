using System.Globalization;
using System.Net;

namespace MIP.Aws.Infrastructure.Operator;

public sealed record PdfDownloadMailItem(
    string SourceName,
    string FileName,
    DateOnly EditionDate,
    long SizeBytes,
    string ViewUrl,
    string DownloadUrl);

public static class PdfDownloadMailHtmlBuilder
{
    public static string Build(
        IReadOnlyList<PdfDownloadMailItem> items,
        string downloadAllUrl,
        string logoUrl)
    {
        var rows = string.Join(
            Environment.NewLine,
            items.Select(BuildRow));

        return $$"""
            <!doctype html>
            <html>
            <body style="margin:0;background:#fff;color:#6d6d6d;font-family:Arial,Helvetica,sans-serif;">
              <div style="max-width:1024px;margin:0 auto;border:1px solid #eee;">
                <div style="height:66px;border-bottom:1px solid #e8e8e8;padding:0 12px;display:flex;align-items:center;">
                  <img src="{{Encode(logoUrl)}}" alt="GFH" width="132" height="54" style="display:block;object-fit:contain;">
                  <a href="{{Encode(downloadAllUrl)}}" style="margin-left:auto;background:#20364d;color:#fff;text-decoration:none;border-radius:3px;padding:11px 16px;font-size:14px;font-weight:700;">&#x21E9; Download All</a>
                </div>
                <div style="padding:5px 12px 145px 48px;">
                  <p style="color:#999;font-size:11px;margin:8px 0 2px;">These secure download links expire after 24 hours.</p>
            {{rows}}
                </div>
                <div style="border-top:1px solid #eee;padding:12px 64px;color:#777;font-size:11px;display:flex;justify-content:space-between;gap:12px;">
                  <span>&copy; 2024 GFH Financial Group, all rights reserved.</span>
                  <span>Sent by: <strong>GFH News Papers</strong></span>
                </div>
              </div>
            </body>
            </html>
            """;
    }

    private static string BuildRow(PdfDownloadMailItem item) =>
        $$"""
                  <div style="display:flex;align-items:center;min-height:67px;border-bottom:1px dotted #e5e5e5;gap:12px;">
                    <span style="color:#e23b59;font-size:18px;">□</span>
                    <a href="{{Encode(item.ViewUrl)}}" style="flex:1;color:#777;text-decoration:none;font-size:14px;font-weight:700;">
                      <span style="display:block;color:#777;font-size:10px;font-weight:400;margin-bottom:2px;">{{Encode(item.SourceName)}}</span>
                      {{Encode(item.FileName)}}
                      <span style="display:block;color:#999;font-size:11px;font-weight:400;margin-top:4px;">
                        {{item.EditionDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}} &nbsp;|&nbsp; {{FormatSize(item.SizeBytes)}}
                      </span>
                    </a>
                    <a href="{{Encode(item.DownloadUrl)}}" style="background:#eee;color:#777;text-decoration:none;border-radius:4px;padding:7px 13px;font-size:12px;font-weight:700;">Download</a>
                  </div>
            """;

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var megabytes = bytes / (1024d * 1024d);
        return $"{megabytes:0.##} MB";
    }
}
