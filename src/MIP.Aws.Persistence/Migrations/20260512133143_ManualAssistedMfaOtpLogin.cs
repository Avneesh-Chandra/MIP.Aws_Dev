using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManualAssistedMfaOtpLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssistedSessionTimeoutMinutes",
                table: "NewsSources",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ManualLoginRequired",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OtpInstructions",
                table: "NewsSources",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresMfa",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresOtp",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PortalManualLoginSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedByEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SessionArtifactRelativePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_PortalManualLoginSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortalManualLoginSessions_NewsSources_NewsSourceId",
                        column: x => x.NewsSourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortalManualLoginSessions_NewsSourceId_ExpiresAt",
                table: "PortalManualLoginSessions",
                columns: new[] { "NewsSourceId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PortalManualLoginSessions_NewsSourceId_Status_CreatedAt",
                table: "PortalManualLoginSessions",
                columns: new[] { "NewsSourceId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortalManualLoginSessions");

            migrationBuilder.DropColumn(
                name: "AssistedSessionTimeoutMinutes",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "ManualLoginRequired",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "OtpInstructions",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "RequiresMfa",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "RequiresOtp",
                table: "NewsSources");
        }
    }
}
