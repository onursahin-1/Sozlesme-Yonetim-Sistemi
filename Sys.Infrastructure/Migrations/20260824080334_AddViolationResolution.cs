using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddViolationResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolutionNote",
                table: "Violations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Violations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolvedByUserId",
                table: "Violations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolutionNote",
                table: "Violations");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Violations");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "Violations");
        }
    }
}
