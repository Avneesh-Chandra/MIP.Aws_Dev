using MIP.Aws.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations;

[DbContext(typeof(MediaIntelligenceDbContext))]
[Migration("20260907120000_AddPdfDownloadMailDesign")]
public partial class AddPdfDownloadMailDesign : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PdfDownloadMailDesign",
            table: "MailSettings",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PdfDownloadMailDesign",
            table: "MailSettings");
    }
}
