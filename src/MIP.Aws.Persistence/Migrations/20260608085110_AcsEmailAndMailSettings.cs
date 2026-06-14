using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AcsEmailAndMailSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BriefId",
                table: "EmailLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderOperationId",
                table: "EmailLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MailSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActiveProvider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DevelopmentSafetyEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RedirectAllTo = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    SubjectPrefix = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
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
                    table.PrimaryKey("PK_MailSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_BriefId",
                table: "EmailLogs",
                column: "BriefId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailSettings");

            migrationBuilder.DropIndex(
                name: "IX_EmailLogs_BriefId",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "BriefId",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "ProviderOperationId",
                table: "EmailLogs");
        }
    }
}
