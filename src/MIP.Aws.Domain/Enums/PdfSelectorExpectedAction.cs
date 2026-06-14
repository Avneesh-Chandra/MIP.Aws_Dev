namespace MIP.Aws.Domain.Enums;

public enum PdfSelectorExpectedAction
{
    ExtractHref = 0,
    ClickAndWaitForDownload = 1,
    ClickAndWaitForPopup = 2,
    InspectParentAnchor = 3
}
