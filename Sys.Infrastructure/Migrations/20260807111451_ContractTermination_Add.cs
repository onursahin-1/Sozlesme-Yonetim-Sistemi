using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ContractTermination_Add : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PendingTermination",
                table: "Contracts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ContractTerminations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    TerminationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TerminationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompensationAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CompensationDirection = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTerminations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractTerminations_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractTerminations_ContractId",
                table: "ContractTerminations",
                column: "ContractId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractTerminations");

            migrationBuilder.DropColumn(
                name: "PendingTermination",
                table: "Contracts");
        }
    }
}
