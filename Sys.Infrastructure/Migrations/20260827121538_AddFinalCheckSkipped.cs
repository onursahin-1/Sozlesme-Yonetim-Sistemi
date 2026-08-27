using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalCheckSkipped : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FinalCheckSkipped",
                table: "Contracts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalCheckSkipped",
                table: "Contracts");
        }
    }
}
