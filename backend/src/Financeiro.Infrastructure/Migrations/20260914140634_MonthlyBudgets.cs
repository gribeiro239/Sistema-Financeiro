using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financeiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MonthlyBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonthlyBudgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetKind = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlannedAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AlertPercentage = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyBudgets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyBudgets_WorkspaceId_Year_Month",
                table: "MonthlyBudgets",
                columns: new[] { "WorkspaceId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyBudgets_WorkspaceId_Year_Month_TargetKind_TargetId",
                table: "MonthlyBudgets",
                columns: new[] { "WorkspaceId", "Year", "Month", "TargetKind", "TargetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthlyBudgets");
        }
    }
}
