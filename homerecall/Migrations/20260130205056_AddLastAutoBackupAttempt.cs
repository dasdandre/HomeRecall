using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeRecall.Migrations
{
    /// <inheritdoc />
    public partial class AddLastAutoBackupAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastAutoBackupAttempt",
                table: "Devices",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastAutoBackupAttempt",
                table: "Devices");
        }
    }
}
