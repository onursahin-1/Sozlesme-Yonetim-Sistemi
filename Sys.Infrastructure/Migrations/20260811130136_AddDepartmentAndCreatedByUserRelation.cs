using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentAndCreatedByUserRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CreatedByUserId",
                table: "Contracts",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Users_CreatedByUserId",
                table: "Contracts",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Users_CreatedByUserId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_CreatedByUserId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Users");
        }
    }
}
