using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeRecall.Migrations
{
    /// <inheritdoc />
    public partial class AddMdnsEnabledToAppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MdnsEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                column: "MdnsEnabled",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MdnsEnabled",
                table: "Settings");
        }
    }
}
