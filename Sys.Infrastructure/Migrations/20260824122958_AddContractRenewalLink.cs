using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractRenewalLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RenewedFromContractId",
                table: "Contracts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_RenewedFromContractId",
                table: "Contracts",
                column: "RenewedFromContractId",
                filter: "[RenewedFromContractId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_RenewedFromContractId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "RenewedFromContractId",
                table: "Contracts");
        }
    }
}
