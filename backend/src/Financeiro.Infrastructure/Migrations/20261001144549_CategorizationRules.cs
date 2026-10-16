using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financeiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CategorizationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategorizationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    DescriptionPattern = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    MatchMode = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionType = table.Column<int>(type: "INTEGER", nullable: true),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CostCenterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategorizationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategorizationRules_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CategorizationRules_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategorizationRules_AccountId",
                table: "CategorizationRules",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CategorizationRules_CostCenterId",
                table: "CategorizationRules",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_CategorizationRules_WorkspaceId_IsActive",
                table: "CategorizationRules",
                columns: new[] { "WorkspaceId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CategorizationRules_WorkspaceId_Priority",
                table: "CategorizationRules",
                columns: new[] { "WorkspaceId", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategorizationRules");
        }
    }
}
