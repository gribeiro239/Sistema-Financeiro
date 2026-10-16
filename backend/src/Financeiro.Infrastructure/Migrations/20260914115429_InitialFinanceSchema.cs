using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financeiro.Infrastructure.Migrations;

public partial class InitialFinanceSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // IF NOT EXISTS permite adotar sem perda os bancos criados antes das migrations.
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "Accounts" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Accounts" PRIMARY KEY,
                "WorkspaceId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Type" INTEGER NOT NULL,
                "Institution" TEXT NOT NULL,
                "LastFourDigits" TEXT NULL,
                "OpeningBalance" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "Commitments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Commitments" PRIMARY KEY,
                "WorkspaceId" TEXT NOT NULL,
                "SeriesId" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "Contact" TEXT NOT NULL,
                "Category" TEXT NOT NULL,
                "Kind" INTEGER NOT NULL,
                "TotalAmount" TEXT NOT NULL,
                "SettledAmount" TEXT NOT NULL,
                "DueDate" TEXT NOT NULL,
                "CompetenceDate" TEXT NOT NULL,
                "InstallmentNumber" INTEGER NOT NULL,
                "InstallmentCount" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "Transactions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Transactions" PRIMARY KEY,
                "WorkspaceId" TEXT NOT NULL,
                "AccountId" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "Category" TEXT NOT NULL,
                "Type" INTEGER NOT NULL,
                "Amount" TEXT NOT NULL,
                "OccurredOn" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_Transactions_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_Accounts_WorkspaceId_IsActive" ON "Accounts" ("WorkspaceId", "IsActive");
            CREATE INDEX IF NOT EXISTS "IX_Commitments_SeriesId" ON "Commitments" ("SeriesId");
            CREATE INDEX IF NOT EXISTS "IX_Commitments_WorkspaceId_DueDate" ON "Commitments" ("WorkspaceId", "DueDate");
            CREATE INDEX IF NOT EXISTS "IX_Commitments_WorkspaceId_Status_Kind" ON "Commitments" ("WorkspaceId", "Status", "Kind");
            CREATE INDEX IF NOT EXISTS "IX_Transactions_AccountId" ON "Transactions" ("AccountId");
            CREATE INDEX IF NOT EXISTS "IX_Transactions_WorkspaceId_OccurredOn" ON "Transactions" ("WorkspaceId", "OccurredOn");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Commitments");
        migrationBuilder.DropTable(name: "Transactions");
        migrationBuilder.DropTable(name: "Accounts");
    }
}
