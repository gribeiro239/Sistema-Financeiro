using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financeiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TransfersAndRecurringCommitments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CounterpartyAccountId",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferId",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceCount",
                table: "Commitments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceFrequency",
                table: "Commitments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceNumber",
                table: "Commitments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CounterpartyAccountId",
                table: "Transactions",
                column: "CounterpartyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransferId",
                table: "Transactions",
                column: "TransferId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_CounterpartyAccountId",
                table: "Transactions",
                column: "CounterpartyAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_CounterpartyAccountId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_CounterpartyAccountId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransferId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CounterpartyAccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TransferId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RecurrenceCount",
                table: "Commitments");

            migrationBuilder.DropColumn(
                name: "RecurrenceFrequency",
                table: "Commitments");

            migrationBuilder.DropColumn(
                name: "RecurrenceNumber",
                table: "Commitments");
        }
    }
}
