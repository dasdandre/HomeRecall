using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeRecall.Migrations
{
    /// <inheritdoc />
    public partial class AddMqttSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MqttAutoAdd",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MqttEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MqttHost",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MqttPasswordEncrypted",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MqttPort",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MqttUsername",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "MqttAutoAdd", "MqttEnabled", "MqttHost", "MqttPasswordEncrypted", "MqttPort", "MqttUsername" },
                values: new object[] { false, false, null, null, 1883, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MqttAutoAdd",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "MqttEnabled",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "MqttHost",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "MqttPasswordEncrypted",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "MqttPort",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "MqttUsername",
                table: "Settings");
        }
    }
}
