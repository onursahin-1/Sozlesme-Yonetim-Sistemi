using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLastRejectedStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastRejectedStage",
                table: "Contracts",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRejectedStage",
                table: "Contracts");
        }
    }
}
