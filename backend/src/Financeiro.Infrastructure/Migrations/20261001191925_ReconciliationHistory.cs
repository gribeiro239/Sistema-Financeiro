using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financeiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconciliationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReconciliationHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BankStatementEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TransactionWasCreated = table.Column<bool>(type: "INTEGER", nullable: false),
                    TransactionRemoved = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    CostCenterName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RuleName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationHistoryEntries_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReconciliationHistoryEntries_BankStatementEntries_BankStatementEntryId",
                        column: x => x.BankStatementEntryId,
                        principalTable: "BankStatementEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReconciliationHistoryEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationHistoryEntries_AccountId",
                table: "ReconciliationHistoryEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationHistoryEntries_BankStatementEntryId_OccurredAt",
                table: "ReconciliationHistoryEntries",
                columns: new[] { "BankStatementEntryId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationHistoryEntries_UserId",
                table: "ReconciliationHistoryEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationHistoryEntries_WorkspaceId_OccurredAt",
                table: "ReconciliationHistoryEntries",
                columns: new[] { "WorkspaceId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReconciliationHistoryEntries");
        }
    }
}
