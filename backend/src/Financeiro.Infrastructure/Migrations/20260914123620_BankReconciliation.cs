using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financeiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BankReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankStatementEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImportKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReconciledAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementEntries_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementEntries_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementEntries_AccountId",
                table: "BankStatementEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementEntries_TransactionId",
                table: "BankStatementEntries",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementEntries_WorkspaceId_AccountId_ImportKey",
                table: "BankStatementEntries",
                columns: new[] { "WorkspaceId", "AccountId", "ImportKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementEntries_WorkspaceId_Status_OccurredOn",
                table: "BankStatementEntries",
                columns: new[] { "WorkspaceId", "Status", "OccurredOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankStatementEntries");
        }
    }
}
