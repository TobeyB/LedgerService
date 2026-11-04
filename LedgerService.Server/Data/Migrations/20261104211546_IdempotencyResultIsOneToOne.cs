using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class IdempotencyResultIsOneToOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecords_ResultTransactionId",
                table: "IdempotencyRecords");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_ResultTransactionId",
                table: "IdempotencyRecords",
                column: "ResultTransactionId",
                unique: true,
                filter: "[ResultTransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecords_ResultTransactionId",
                table: "IdempotencyRecords");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_ResultTransactionId",
                table: "IdempotencyRecords",
                column: "ResultTransactionId");
        }
    }
}
