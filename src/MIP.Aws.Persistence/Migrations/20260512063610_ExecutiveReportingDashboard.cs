using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExecutiveReportingDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Reports",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Reports",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GeneratedAt",
                table: "Reports",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GenerationStatus",
                table: "Reports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "Reports",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IntelligenceAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlertType = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedDownloadJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedOcrJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_IntelligenceAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReportType = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RunAtLocal = table.Column<TimeOnly>(type: "time", nullable: false),
                    TargetRoleName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NextRunUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastRunUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_ReportSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportDeliveryLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RecipientsSnapshot = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_ReportDeliveryLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportDeliveryLogs_ReportSchedules_ReportScheduleId",
                        column: x => x.ReportScheduleId,
                        principalTable: "ReportSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReportDeliveryLogs_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReportScheduleRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("PK_ReportScheduleRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportScheduleRecipients_ReportSchedules_ReportScheduleId",
                        column: x => x.ReportScheduleId,
                        principalTable: "ReportSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntelligenceAlerts_IsAcknowledged_CreatedAt",
                table: "IntelligenceAlerts",
                columns: new[] { "IsAcknowledged", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportDeliveryLogs_ReportId",
                table: "ReportDeliveryLogs",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDeliveryLogs_ReportScheduleId_StartedAt",
                table: "ReportDeliveryLogs",
                columns: new[] { "ReportScheduleId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportScheduleRecipients_ReportScheduleId_Email",
                table: "ReportScheduleRecipients",
                columns: new[] { "ReportScheduleId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_IsEnabled_NextRunUtc",
                table: "ReportSchedules",
                columns: new[] { "IsEnabled", "NextRunUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntelligenceAlerts");

            migrationBuilder.DropTable(
                name: "ReportDeliveryLogs");

            migrationBuilder.DropTable(
                name: "ReportScheduleRecipients");

            migrationBuilder.DropTable(
                name: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "GeneratedAt",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "GenerationStatus",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "Reports");
        }
    }
}
