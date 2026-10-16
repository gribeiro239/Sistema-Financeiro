using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financeiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialSettlementIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CommitmentId",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CommitmentId",
                table: "Transactions",
                column: "CommitmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Commitments_CommitmentId",
                table: "Transactions",
                column: "CommitmentId",
                principalTable: "Commitments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Preserve existing settlement links recorded in the financial audit trail.
            migrationBuilder.Sql("""
                UPDATE Transactions
                SET CommitmentId = (
                    SELECT c.Id FROM AuditEntries a
                    JOIN Commitments c ON c.Id = a.EntityId AND c.WorkspaceId = a.WorkspaceId
                    WHERE a.Action = 'Settled' AND a.EntityType = 'Commitment'
                      AND a.WorkspaceId = Transactions.WorkspaceId
                      AND CASE WHEN json_valid(a.Details) THEN lower(json_extract(a.Details, '$.TransactionId')) ELSE NULL END = lower(Transactions.Id)
                    LIMIT 1
                )
                WHERE CommitmentId IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Commitments_CommitmentId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_CommitmentId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CommitmentId",
                table: "Transactions");
        }
    }
}
