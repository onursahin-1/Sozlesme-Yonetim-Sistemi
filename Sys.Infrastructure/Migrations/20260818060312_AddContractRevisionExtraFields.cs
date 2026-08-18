using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractRevisionExtraFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousCompanyName",
                table: "ContractRevisions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreviousPaymentPeriod",
                table: "ContractRevisions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousTaxNo",
                table: "ContractRevisions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousCompanyName",
                table: "ContractRevisions");

            migrationBuilder.DropColumn(
                name: "PreviousPaymentPeriod",
                table: "ContractRevisions");

            migrationBuilder.DropColumn(
                name: "PreviousTaxNo",
                table: "ContractRevisions");
        }
    }
}
