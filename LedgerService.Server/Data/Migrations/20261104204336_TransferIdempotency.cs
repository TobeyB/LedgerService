using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class TransferIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResponseBody",
                table: "IdempotencyRecords");

            migrationBuilder.RenameColumn(
                name: "ResponseStatusCode",
                table: "IdempotencyRecords",
                newName: "ResultError");

            migrationBuilder.AddColumn<Guid>(
                name: "ResultTransactionId",
                table: "IdempotencyRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_ResultTransactionId",
                table: "IdempotencyRecords",
                column: "ResultTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_IdempotencyRecords_Transactions_ResultTransactionId",
                table: "IdempotencyRecords",
                column: "ResultTransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdempotencyRecords_Transactions_ResultTransactionId",
                table: "IdempotencyRecords");

            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecords_ResultTransactionId",
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "ResultTransactionId",
                table: "IdempotencyRecords");

            migrationBuilder.RenameColumn(
                name: "ResultError",
                table: "IdempotencyRecords",
                newName: "ResponseStatusCode");

            migrationBuilder.AddColumn<string>(
                name: "ResponseBody",
                table: "IdempotencyRecords",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
