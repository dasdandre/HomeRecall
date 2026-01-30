using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeRecall.Migrations
{
    /// <inheritdoc />
    public partial class AddFirmwareVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentFirmwareVersion",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirmwareVersion",
                table: "Backups",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentFirmwareVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "FirmwareVersion",
                table: "Backups");
        }
    }
}
