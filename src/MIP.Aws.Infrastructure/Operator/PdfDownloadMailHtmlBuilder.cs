using System.Globalization;
using System.Net;

namespace MIP.Aws.Infrastructure.Operator;

public sealed record PdfDownloadMailItem(
    string SourceName,
    string FileName,
    DateOnly EditionDate,
    long SizeBytes,
    string ViewUrl,
    string DownloadUrl,
    bool IsAvailable = true);

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
        var editionDate = items.Count == 0
            ? string.Empty
            : items[0].EditionDate.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture);
        var availableCount = items.Count(item => item.IsAvailable);
        var unavailableCount = items.Count - availableCount;
        var editionSummary = unavailableCount == 0
            ? $"{availableCount} monitored editions were downloaded successfully"
            : $"{availableCount} monitored editions are available and {unavailableCount} are marked Not Available";

        return $$"""
            <!doctype html>
            <html lang="en">
            <body style="margin:0;padding:0;background:#eef2f6;color:#263746;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#eef2f6;">
                <tr>
                  <td align="center" style="padding:28px 12px;">
                    <table role="presentation" width="680" cellpadding="0" cellspacing="0" border="0" style="width:100%;max-width:680px;background:#fff;border:1px solid #dce3ea;">
                      <tr>
                        <td style="padding:0;background:#102a43;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                            <tr>
                              <td style="padding:22px 24px;">
                                <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                                  <tr>
                                    <td valign="middle" style="padding-right:12px;">
                                      <div style="width:38px;height:38px;line-height:38px;text-align:center;background:#e31e4d;color:#fff;font-size:22px;font-weight:700;">◆</div>
                                    </td>
                                    <td valign="middle">
                                      <div style="color:#fff;font-size:25px;line-height:25px;font-weight:700;letter-spacing:1px;">GFH</div>
                                      <div style="color:#c7d7e5;font-size:10px;line-height:15px;font-weight:700;letter-spacing:2px;">MEDIA INTELLIGENCE</div>
                                    </td>
                                  </tr>
                                </table>
                              </td>
                              <td align="right" valign="middle" style="padding:22px 24px;">
                                {{DownloadAllButton(downloadAllUrl)}}
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:30px 32px 18px;">
                          <div style="color:#e31e4d;font-size:11px;font-weight:700;letter-spacing:1.4px;text-transform:uppercase;">Daily media briefing</div>
                          <div style="padding-top:7px;color:#102a43;font-size:25px;line-height:31px;font-weight:700;">Your newspaper editions are ready</div>
                          <div style="padding-top:8px;color:#657687;font-size:13px;line-height:20px;">{{Encode(editionSummary)}}
                            for {{Encode(editionDate)}}.</div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:0 32px 14px;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#fff8fa;border:1px solid #f2d5dd;">
                            <tr>
                              <td style="padding:12px 14px;color:#6b4a54;font-size:11px;line-height:17px;">
                                <strong style="color:#e31e4d;">SECURE ACCESS</strong><br>
                                Available links open without a login and expire automatically after 24 hours.
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:0 32px 24px;">
            {{rows}}
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:20px 32px;background:#102a43;border-top:4px solid #e31e4d;">
                          <div style="color:#fff;font-size:15px;font-weight:700;letter-spacing:.2px;">GFH Media Intelligence</div>
                          <div style="padding-top:5px;color:#c7d7e5;font-size:11px;line-height:17px;">Intelligence delivered, one edition at a time.</div>
                          <div style="padding-top:12px;color:#8fa8bb;font-size:10px;line-height:15px;">GFH Financial Group &nbsp;|&nbsp; Media Intelligence Desk<br>This automated message was generated after the daily edition download completed.</div>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildRow(PdfDownloadMailItem item) =>
        item.IsAvailable
            ? $$"""
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:10px;border:1px solid #dce3ea;background:#fff;">
                    <tr>
                      <td width="5" style="width:5px;background:#e31e4d;font-size:1px;line-height:1px;">&nbsp;</td>
                      <td style="padding:16px 14px;">
                        <div style="color:#e31e4d;font-size:10px;font-weight:700;letter-spacing:1px;text-transform:uppercase;">{{Encode(item.SourceName)}}</div>
                        <a href="{{Encode(item.ViewUrl)}}" style="display:block;padding-top:5px;color:#102a43;text-decoration:none;font-size:15px;line-height:20px;font-weight:700;">{{Encode(item.FileName)}}</a>
                        <div style="padding-top:6px;color:#718293;font-size:11px;line-height:16px;">Edition date: {{item.EditionDate.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)}} &nbsp;&bull;&nbsp; {{FormatSize(item.SizeBytes)}}</div>
                      </td>
                      <td align="right" valign="middle" style="padding:16px 14px;white-space:nowrap;">
                        <a href="{{Encode(item.ViewUrl)}}" style="display:inline-block;color:#102a43;text-decoration:none;border:1px solid #b9c8d5;border-radius:4px;padding:8px 10px;font-size:11px;font-weight:700;">OPEN</a>
                        <a href="{{Encode(item.DownloadUrl)}}" style="display:inline-block;margin-left:5px;background:#e31e4d;color:#fff;text-decoration:none;border-radius:4px;padding:9px 11px;font-size:11px;font-weight:700;">DOWNLOAD</a>
                      </td>
                    </tr>
                  </table>
            """
            : $$"""
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:10px;border:1px solid #dce3ea;background:#fff;">
                    <tr>
                      <td width="5" style="width:5px;background:#9aa8b5;font-size:1px;line-height:1px;">&nbsp;</td>
                      <td style="padding:16px 14px;">
                        <div style="color:#718293;font-size:10px;font-weight:700;letter-spacing:1px;text-transform:uppercase;">{{Encode(item.SourceName)}}</div>
                        <div style="padding-top:5px;color:#526273;font-size:15px;line-height:20px;font-weight:700;">Not Available</div>
                        <div style="padding-top:6px;color:#718293;font-size:11px;line-height:16px;">No PDF was stored for this source on the edition date.</div>
                      </td>
                    </tr>
                  </table>
            """;

    private static string DownloadAllButton(string? downloadAllUrl) =>
        string.IsNullOrWhiteSpace(downloadAllUrl)
            ? string.Empty
            : $"<a href=\"{Encode(downloadAllUrl)}\" style=\"display:inline-block;background:#e31e4d;color:#fff;text-decoration:none;border-radius:4px;padding:12px 16px;font-size:12px;font-weight:700;letter-spacing:.3px;\">DOWNLOAD ALL &nbsp;&#x2192;</a>";

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
