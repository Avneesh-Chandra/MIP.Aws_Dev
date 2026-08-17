using MIP.Aws.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations;

[DbContext(typeof(MediaIntelligenceDbContext))]
[Migration("20260817153000_PortalDownloadAuditLogsJobCreatedAt")]
public partial class PortalDownloadAuditLogsJobCreatedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PortalDownloadAuditLogs_DownloadJobId",
            table: "PortalDownloadAuditLogs");

        migrationBuilder.CreateIndex(
            name: "IX_PortalDownloadAuditLogs_DownloadJobId_CreatedAt",
            table: "PortalDownloadAuditLogs",
            columns: new[] { "DownloadJobId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PortalDownloadAuditLogs_DownloadJobId_CreatedAt",
            table: "PortalDownloadAuditLogs");

        migrationBuilder.CreateIndex(
            name: "IX_PortalDownloadAuditLogs_DownloadJobId",
            table: "PortalDownloadAuditLogs",
            column: "DownloadJobId");
    }
}
