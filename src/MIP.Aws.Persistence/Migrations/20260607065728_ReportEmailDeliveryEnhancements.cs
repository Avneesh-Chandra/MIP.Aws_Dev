using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportEmailDeliveryEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bcc",
                table: "EmailLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cc",
                table: "EmailLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromEmail",
                table: "EmailLogs",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MessageId",
                table: "EmailLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalRecipients",
                table: "EmailLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "EmailLogs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ReportScheduleId",
                table: "EmailLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_ReportScheduleId",
                table: "EmailLogs",
                column: "ReportScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_Status",
                table: "EmailLogs",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailLogs_ReportSchedules_ReportScheduleId",
                table: "EmailLogs",
                column: "ReportScheduleId",
                principalTable: "ReportSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailLogs_ReportSchedules_ReportScheduleId",
                table: "EmailLogs");

            migrationBuilder.DropIndex(
                name: "IX_EmailLogs_ReportScheduleId",
                table: "EmailLogs");

            migrationBuilder.DropIndex(
                name: "IX_EmailLogs_Status",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "Bcc",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "Cc",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "FromEmail",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "OriginalRecipients",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "ReportScheduleId",
                table: "EmailLogs");
        }
    }
}
