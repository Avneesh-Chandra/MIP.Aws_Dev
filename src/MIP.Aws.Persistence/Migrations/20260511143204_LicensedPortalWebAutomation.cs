using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LicensedPortalWebAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProtectedCredentialPayload",
                table: "SourceCredentials",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DownloadSelector",
                table: "NewsSources",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditionUrl",
                table: "NewsSources",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDownloadAllowed",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LoginMethod",
                table: "NewsSources",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "LoginSuccessSelector",
                table: "NewsSources",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoginUrl",
                table: "NewsSources",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoutUrl",
                table: "NewsSources",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "NewsSources",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordSelector",
                table: "NewsSources",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortalUsername",
                table: "NewsSources",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresCaptcha",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresLogin",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresManualAction",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SourceAccessMode",
                table: "NewsSources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SubmitSelector",
                table: "NewsSources",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuccessUrlPattern",
                table: "NewsSources",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsernameSelector",
                table: "NewsSources",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PortalDownloadAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DownloadJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventKind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ScreenshotRelativePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    HtmlSnapshotRelativePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    HttpStatus = table.Column<int>(type: "int", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalDownloadAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortalDownloadAuditLogs_DownloadJobs_DownloadJobId",
                        column: x => x.DownloadJobId,
                        principalTable: "DownloadJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PortalDownloadAuditLogs_NewsSources_NewsSourceId",
                        column: x => x.NewsSourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceIngestionAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlertType = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceIngestionAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceIngestionAlerts_NewsSources_NewsSourceId",
                        column: x => x.NewsSourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortalDownloadAuditLogs_DownloadJobId",
                table: "PortalDownloadAuditLogs",
                column: "DownloadJobId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalDownloadAuditLogs_NewsSourceId_CreatedAt",
                table: "PortalDownloadAuditLogs",
                columns: new[] { "NewsSourceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceIngestionAlerts_NewsSourceId_IsResolved_CreatedAt",
                table: "SourceIngestionAlerts",
                columns: new[] { "NewsSourceId", "IsResolved", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortalDownloadAuditLogs");

            migrationBuilder.DropTable(
                name: "SourceIngestionAlerts");

            migrationBuilder.DropColumn(
                name: "DownloadSelector",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "EditionUrl",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "IsDownloadAllowed",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "LoginMethod",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "LoginSuccessSelector",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "LoginUrl",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "LogoutUrl",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PasswordSelector",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "PortalUsername",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "RequiresCaptcha",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "RequiresLogin",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "RequiresManualAction",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "SourceAccessMode",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "SubmitSelector",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "SuccessUrlPattern",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "UsernameSelector",
                table: "NewsSources");

            migrationBuilder.AlterColumn<string>(
                name: "ProtectedCredentialPayload",
                table: "SourceCredentials",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
