using MIP.Aws.Application.Features.NewsSources.PdfEdition;
using MIP.Aws.Domain.Security;

namespace MIP.Aws.Tests;

public sealed class ManualPdfUploadTests
{
    [Fact]
    public void Validate_accepts_pdf_with_configured_minimum_size()
    {
        var content = new byte[1024];
        "%PDF-1.7"u8.CopyTo(content);

        var error = PdfManualUploadRules.Validate("edition.pdf", content, minimumSizeKb: 1);

        Assert.Null(error);
    }

    [Theory]
    [InlineData("edition.txt", "Only PDF files can be uploaded.")]
    [InlineData("edition.pdf", "The uploaded file is not a valid PDF.")]
    public void Validate_rejects_invalid_uploads(string fileName, string expectedError)
    {
        var content = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "not a pdf"u8.ToArray()
            : "%PDF-1.7"u8.ToArray();

        var error = PdfManualUploadRules.Validate(fileName, content, minimumSizeKb: 0);

        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void NormalizeFileName_removes_path_components_and_adds_pdf_extension()
    {
        var normalized = PdfManualUploadRules.NormalizeFileName(@"..\private\edition");

        Assert.Equal("edition.pdf", normalized);
    }

    [Fact]
    public void Manual_pdf_upload_permission_is_granted_only_to_operator_and_super_admin()
    {
        Assert.Contains(
            ApplicationPermissions.ManualPdfUpload,
            RolePermissionMatrix.GetPermissionsForRole(ApplicationRoles.MIPOperator));
        Assert.Contains(
            ApplicationPermissions.ManualPdfUpload,
            RolePermissionMatrix.GetPermissionsForRole(ApplicationRoles.SuperAdmin));
        Assert.DoesNotContain(
            ApplicationPermissions.ManualPdfUpload,
            RolePermissionMatrix.GetPermissionsForRole(ApplicationRoles.Analyst));
    }
}
