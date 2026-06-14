using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MarketSnapshotCaptureMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CapturedAt",
                table: "MarketPriceSnapshots",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DataDelayMinutes",
                table: "MarketPriceSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarketStatus",
                table: "MarketPriceSnapshots",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizationNotes",
                table: "MarketPriceSnapshots",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawPayloadJson",
                table: "MarketPriceSnapshots",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "MarketPriceSnapshots",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapturedAt",
                table: "MarketPriceSnapshots");

            migrationBuilder.DropColumn(
                name: "DataDelayMinutes",
                table: "MarketPriceSnapshots");

            migrationBuilder.DropColumn(
                name: "MarketStatus",
                table: "MarketPriceSnapshots");

            migrationBuilder.DropColumn(
                name: "NormalizationNotes",
                table: "MarketPriceSnapshots");

            migrationBuilder.DropColumn(
                name: "RawPayloadJson",
                table: "MarketPriceSnapshots");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "MarketPriceSnapshots");
        }
    }
}
