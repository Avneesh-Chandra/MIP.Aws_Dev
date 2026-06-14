using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MIPOperatorDownloadMonitor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminInterventionNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DownloadJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SuggestedAction = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OperatorNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AdminInterventionNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminInterventionNotifications_DownloadJobs_DownloadJobId",
                        column: x => x.DownloadJobId,
                        principalTable: "DownloadJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AdminInterventionNotifications_NewsSources_NewsSourceId",
                        column: x => x.NewsSourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DownloadOperatorNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DownloadJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_DownloadOperatorNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadOperatorNotes_DownloadJobs_DownloadJobId",
                        column: x => x.DownloadJobId,
                        principalTable: "DownloadJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadOperatorNotes_NewsSources_NewsSourceId",
                        column: x => x.NewsSourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminInterventionNotifications_DownloadJobId",
                table: "AdminInterventionNotifications",
                column: "DownloadJobId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminInterventionNotifications_NewsSourceId",
                table: "AdminInterventionNotifications",
                column: "NewsSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminInterventionNotifications_Status_CreatedAt",
                table: "AdminInterventionNotifications",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadOperatorNotes_DownloadJobId",
                table: "DownloadOperatorNotes",
                column: "DownloadJobId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadOperatorNotes_NewsSourceId_CreatedAt",
                table: "DownloadOperatorNotes",
                columns: new[] { "NewsSourceId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminInterventionNotifications");

            migrationBuilder.DropTable(
                name: "DownloadOperatorNotes");
        }
    }
}
