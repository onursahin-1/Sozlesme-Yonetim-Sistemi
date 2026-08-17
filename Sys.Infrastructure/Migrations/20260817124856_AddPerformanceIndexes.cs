using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_CreatedByUserId",
                table: "Contracts");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CreatedByUserId_Status",
                table: "Contracts",
                columns: new[] { "CreatedByUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_EndDate",
                table: "Contracts",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Stage",
                table: "Contracts",
                column: "Stage");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Status",
                table: "Contracts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActionDate",
                table: "AuditLogs",
                column: "ActionDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_CreatedByUserId_Status",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_EndDate",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_Stage",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_Status",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ActionDate",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CreatedByUserId",
                table: "Contracts",
                column: "CreatedByUserId");
        }
    }
}
