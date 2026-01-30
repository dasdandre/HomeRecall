using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeRecall.Migrations
{
    /// <inheritdoc />
    public partial class AddScanSettingsPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastScanDeviceTypes",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastScanIpEndSuffix",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastScanIpStart",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "LastScanDeviceTypes", "LastScanIpEndSuffix", "LastScanIpStart" },
                values: new object[] { null, 254, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastScanDeviceTypes",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "LastScanIpEndSuffix",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "LastScanIpStart",
                table: "Settings");
        }
    }
}
