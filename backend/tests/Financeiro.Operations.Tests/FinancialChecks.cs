using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Financeiro.Application;
using Financeiro.Domain;
using Financeiro.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

internal static class FinancialChecks
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateOnly Today = new(2026, 10, 2);
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    private static async Task Reject<T>(Func<Task> action, string message) where T : Exception
    {
        try { await action(); } catch (T) { assertions++; return; }
        throw new Exception(message);
    }

    public static async Task RunAsync()
    {
        var cases = new (string Name, Func<Fixture, Task> Body)[]
        {
            ("Balances, decimal precision and editing", Balances),
            ("Transfers preserve total and stay outside results", Transfers),
            ("Transfer edits and voids affect both legs", TransferVoid),
            ("Repository writes roll back the entire transfer", AtomicTransfer),
            ("Partial and complete settlements preserve money", Settlements),
            ("Settlement void reopens the commitment", SettlementVoid),
            ("Settlement boundaries and commitment states", SettlementBoundaries),
            ("Stale settlement requests cannot overwrite or overpay", StaleSettlements),
            ("Installments preserve rounded total and monthly dates", Installments),
            ("Recurrences preserve calendar anchors", Recurrences),
            ("Invalid subcent values never persist", MoneyBoundaries),
            ("Calendar uses Sao Paulo day, month and overdue status", Dates),
            ("Archived accounts and cash-flow points stay consistent", CashFlow),
            ("CSV/OFX imports validate dates, signs and cent values", ImportFormats),
            ("Import duplicates are account scoped and culture independent", ImportDuplicates),
            ("Reconciliation does not duplicate existing movements", Reconciliation),
            ("Undo reconciliation preserves or reverses the automatic movement", UndoReconciliation),
            ("Voiding reconciled movements reopens bank entries", VoidReconciliation),
            ("Import batch rejection commits no partial data", AtomicImport),
            ("Workspace isolation rejects foreign entity identifiers", Isolation),
            ("Cost allocation is validated after rounding", Allocations)
        };
        var failures = new List<string>();
        foreach (var test in cases)
        {
            await using var fixture = await Fixture.CreateAsync();
            try { await test.Body(fixture); Console.WriteLine("PASS: " + test.Name); }
            catch (Exception exception) { failures.Add(test.Name + ": " + exception); Console.WriteLine("FAIL: " + test.Name + ": " + exception.Message); }
        }
        try { await LegacyMigration(); Console.WriteLine("PASS: Migration preserves historical settlement links"); }
        catch (Exception exception) { failures.Add("Migration: " + exception); }
        try { await HttpIsolation(); Console.WriteLine("PASS: HTTP authentication, workspace isolation and member permissions"); }
        catch (Exception exception) { failures.Add("HTTP isolation: " + exception); }
        Console.WriteLine($"Financial validation: {assertions} assertions, {failures.Count} failures.");
        if (failures.Count > 0) throw new Exception(string.Join("\n", failures));
    }

    private static async Task Balances(Fixture f)
    {
        var account = await f.Account(f.A, "Principal", 100m);
        var income = await f.Transaction(f.A, account.Id, 10.005m);
        await f.Transaction(f.A, account.Id, .10m, TransactionType.Expense);
        await f.Transaction(f.A, account.Id, .20m, TransactionType.Expense);
        Check(income.Amount == 10.01m && await f.Balance(f.A, account.Id) == 109.71m, "Financial amounts must use cents without floating-point drift");
        await f.In(f.A, service => service.UpdateTransactionAsync(income.Id, new(account.Id, "Receita editada", "Serviços", TransactionType.Income, 20m, Today), Ct));
        Check(await f.Balance(f.A, account.Id) == 119.70m, "Editing must replace the original amount");
        await f.In(f.A, service => service.DeleteTransactionAsync(income.Id, Ct));
        Check(await f.Balance(f.A, account.Id) == 99.70m, "Void must remove only that transaction effect");
        using var db = f.Db();
        Check(await db.Transactions.CountAsync(Ct) == 3 && (await db.Transactions.SingleAsync(item => item.Id == income.Id, Ct)).IsVoided, "Voids must preserve historical records");
    }

    private static async Task Transfers(Fixture f)
    {
        var a = await f.Account(f.A, "Origem", 100m); var b = await f.Account(f.A, "Destino", 50m);
        var legs = await f.Transfer(f.A, a.Id, b.Id, 10.005m);
        Check(legs.Count == 2 && legs[0].TransferId == legs[1].TransferId && legs.Sum(item => item.SignedAmount) == 0m, "Transfer needs two equal linked legs");
        Check(await f.Balance(f.A, a.Id) == 89.99m && await f.Balance(f.A, b.Id) == 60.01m, "Transfer must move exactly the rounded amount");
        var dashboard = await f.In(f.A, service => service.GetDashboardAsync(Ct));
        var dre = await f.In(f.A, service => service.GetDreAsync(2026, 10, null, Ct));
        Check(dashboard.TotalBalance == 150m && dashboard.MonthIncome == 0 && dashboard.MonthExpense == 0 && dre.NetResult == 0, "Transfer must not become revenue or expense");
        await Reject<ArgumentException>(() => f.Transfer(f.A, a.Id, a.Id, 1m), "Same account transfer must fail");
    }

    private static async Task TransferVoid(Fixture f)
    {
        var a = await f.Account(f.A, "Origem", 100m); var b = await f.Account(f.A, "Destino", 50m);
        var transfer = await f.Transfer(f.A, a.Id, b.Id, 10m);
        await f.In(f.A, service => service.UpdateTransferAsync(transfer[0].TransferId!.Value, new(b.Id, a.Id, "Transferência editada", 20m, Today), Ct));
        Check(await f.Balance(f.A, a.Id) == 120m && await f.Balance(f.A, b.Id) == 30m, "Editing can change source, destination and amount without extra legs");
        Check(await f.In(f.A, service => service.DeleteTransactionAsync(transfer[0].Id, Ct)), "Transfer void succeeds");
        Check(await f.Balance(f.A, a.Id) == 100m && await f.Balance(f.A, b.Id) == 50m, "Voiding either leg restores both balances");
        Check(!await f.In(f.A, service => service.DeleteTransactionAsync(transfer[1].Id, Ct)), "Second void has no effect");
        using var db = f.Db();
        Check(await db.Transactions.CountAsync(item => item.IsVoided, Ct) == 2, "Both transfer legs remain as voided history");
    }

    private static async Task AtomicTransfer(Fixture f)
    {
        var account = await f.Account(f.A, "Principal", 100m);
        using var db = f.Db(); var repository = new SqliteFinanceRepository(db);
        var transferId = Guid.NewGuid();
        var valid = new FinancialTransaction { Id = Guid.NewGuid(), WorkspaceId = f.A, AccountId = account.Id, TransferId = transferId,
            Description = "Origem", Category = "Transferência", Type = TransactionType.Expense, Amount = 10m, OccurredOn = Today, CreatedAt = DateTimeOffset.UtcNow };
        var invalid = new FinancialTransaction { Id = Guid.NewGuid(), WorkspaceId = f.A, AccountId = Guid.NewGuid(), TransferId = transferId,
            Description = "Destino", Category = "Transferência", Type = TransactionType.Income, Amount = 10m, OccurredOn = Today, CreatedAt = DateTimeOffset.UtcNow };
        await Reject<DbUpdateException>(() => repository.AddTransactionsAsync([valid, invalid], Ct), "Invalid destination must reject transfer batch");
        using var checkDb = f.Db();
        Check(await checkDb.Transactions.CountAsync(Ct) == 0 && await f.Balance(f.A, account.Id) == 100m, "Failure cannot leave just one leg");
    }

    private static async Task Settlements(Fixture f)
    {
        var account = await f.Account(f.A, "Principal", 100m);
        var payable = (await f.Commitment(f.A, 10m))[0];
        var first = await f.Settle(f.A, payable.Id, account.Id, 3.335m);
        Check(first.SettledAmount == 3.34m && first.RemainingAmount == 6.66m && first.Status == "PartiallySettled", "Partial settlement uses normalized cents");
        var last = await f.Settle(f.A, payable.Id, account.Id, 6.66m);
        Check(last.Status == "Settled" && last.RemainingAmount == 0 && await f.Balance(f.A, account.Id) == 90m, "Final payment must close the commitment exactly");
        await Reject<InvalidOperationException>(() => f.Settle(f.A, payable.Id, account.Id, .01m), "Settled commitment rejects extra payment");
        var receivable = (await f.Commitment(f.A, 20m, kind: CommitmentKind.Receivable))[0];
        await f.Settle(f.A, receivable.Id, account.Id, 20m);
        Check(await f.Balance(f.A, account.Id) == 110m, "Receivable settlement must increase cash");
        using var db = f.Db();
        Check(await db.Transactions.CountAsync(item => item.CommitmentId == payable.Id, Ct) == 2, "Payments keep their commitment link");
    }

    private static async Task SettlementVoid(Fixture f)
    {
        var account = await f.Account(f.A, "Principal", 100m);
        var payable = (await f.Commitment(f.A, 10m))[0];
        await f.Settle(f.A, payable.Id, account.Id, 4m); await f.Settle(f.A, payable.Id, account.Id, 6m);
        TransactionView payment;
        payment = (await f.In(f.A, service => service.GetTransactionsAsync(Ct))).Single(item => item.Amount == 4m);
        await Reject<ArgumentException>(() => f.In(f.A, service => service.UpdateTransactionAsync(payment.Id, new(account.Id, "Alterada", "Serviços", TransactionType.Expense, 5m, Today), Ct)), "Linked payments cannot bypass settlement rules through editing");
        await f.In(f.A, service => service.DeleteTransactionAsync(payment.Id, Ct));
        var reopened = (await f.In(f.A, service => service.GetCommitmentsAsync(Ct))).Single();
        Check(reopened.SettledAmount == 6m && reopened.RemainingAmount == 4m && reopened.Status == "PartiallySettled", "Void of a partial payment restores its remaining amount");
        Check(await f.Balance(f.A, account.Id) == 94m, "Payment void restores the same amount to cash");
        var second = (await f.In(f.A, service => service.GetTransactionsAsync(Ct))).Single();
        await f.In(f.A, service => service.DeleteTransactionAsync(second.Id, Ct));
        reopened = (await f.In(f.A, service => service.GetCommitmentsAsync(Ct))).Single();
        Check(reopened.Status == "Open" && reopened.SettledAmount == 0m, "Voiding every payment reopens commitment fully");
        await f.Settle(f.A, payable.Id, account.Id, 10m);
        Check(await f.Balance(f.A, account.Id) == 90m, "A reopened commitment can be settled again");
    }

    private static async Task SettlementBoundaries(Fixture f)
    {
        var account = await f.Account(f.A, "Principal", 100m); var item = (await f.Commitment(f.A, 10m))[0];
        await Reject<ArgumentException>(() => f.Settle(f.A, item.Id, account.Id, 10.006m), "Normalized payment must not exceed remaining cents");
        await f.Settle(f.A, item.Id, account.Id, 4m);
        await Reject<InvalidOperationException>(() => f.In(f.A, service => service.CancelCommitmentAsync(item.Id, Ct)), "Paid commitments cannot be cancelled");
        await Reject<ArgumentException>(() => f.In(f.A, service => service.UpdateCommitmentAsync(item.Id, new(null, "Conta editada", "Fornecedor", "Serviços", CommitmentKind.Payable, 3.99m, Today, Today), Ct)), "Total cannot be below already paid amount");
        var updated = await f.In(f.A, service => service.UpdateCommitmentAsync(item.Id, new(null, "Conta editada", "Fornecedor", "Serviços", CommitmentKind.Payable, 4m, Today, Today), Ct));
        Check(updated.Status == "Settled" && updated.RemainingAmount == 0m, "Reducing total to the paid amount closes the commitment");
        var cancel = (await f.Commitment(f.A, 1m))[0];
        await f.In(f.A, service => service.CancelCommitmentAsync(cancel.Id, Ct));
        await Reject<InvalidOperationException>(() => f.Settle(f.A, cancel.Id, account.Id, 1m), "Cancelled commitments reject payments");
    }

    private static async Task StaleSettlements(Fixture f)
    {
        var account = await f.Account(f.A, "Principal", 100m); var item = (await f.Commitment(f.A, 100m))[0];
        FinancialCommitment stale;
        using (var db = f.Db()) stale = await db.Commitments.AsNoTracking().SingleAsync(Ct);
        await f.Settle(f.A, item.Id, account.Id, 60m);
        stale.Settle(60m);
        using (var db = f.Db())
        {
            var transaction = new FinancialTransaction { Id = Guid.NewGuid(), WorkspaceId = f.A, AccountId = account.Id, CommitmentId = item.Id,
                Description = "Baixa concorrente", Category = "Serviços", Type = TransactionType.Expense, Amount = 60m, OccurredOn = Today, CreatedAt = DateTimeOffset.UtcNow };
            await Reject<ArgumentException>(() => new SqliteFinanceRepository(db).SettleCommitmentAsync(stale, transaction, Ct), "Stale payment must be revalidated inside write transaction");
        }
        Check(await f.Balance(f.A, account.Id) == 40m, "Rejected stale payment changes neither balance nor paid amount");
        var current = (await f.In(f.A, service => service.GetCommitmentsAsync(Ct))).Single();
        Check(current.SettledAmount == 60m && current.RemainingAmount == 40m, "Previous successful payment is preserved");
    }

    private static async Task Installments(Fixture f)
    {
        var items = await f.Commitment(f.A, 100.005m, installments: 3, due: new(2024, 1, 31));
        Check(items.Sum(item => item.TotalAmount) == 100.01m && items.All(item => item.TotalAmount == FinancialMoney.Round(item.TotalAmount)), "Rounded total must equal sum of cent installments");
        Check(items.Select(item => item.DueDate).SequenceEqual(new DateOnly[] { new(2024, 1, 31), new(2024, 2, 29), new(2024, 3, 31) }), "Monthly dates must use original day anchor including leap year");
        Check(items.Select(item => item.InstallmentNumber).SequenceEqual(new[] { 1, 2, 3 }) && items.Select(item => item.SeriesId).Distinct().Count() == 1, "Installments preserve sequence and series");
        await Reject<ArgumentException>(() => f.Commitment(f.A, .01m, installments: 2), "Installments cannot contain zero-value rows");
    }

    private static async Task Recurrences(Fixture f)
    {
        var monthly = await f.Commitment(f.A, 10.005m, frequency: RecurrenceFrequency.Monthly, count: 3, due: new(2025, 1, 31));
        Check(monthly.All(item => item.TotalAmount == 10.01m) && monthly[2].DueDate == new DateOnly(2025, 3, 31), "Recurrences repeat rounded amount without month-end drift");
        var yearly = await f.Commitment(f.A, 1m, frequency: RecurrenceFrequency.Yearly, count: 5, due: new(2024, 2, 29));
        Check(yearly[1].DueDate == new DateOnly(2025, 2, 28) && yearly[4].DueDate == new DateOnly(2028, 2, 29), "Yearly recurrence restores leap-year anchor");
        await Reject<ArgumentException>(() => f.Commitment(f.A, 1m, installments: 2, frequency: RecurrenceFrequency.Monthly, count: 3), "Installments and recurrences cannot combine");
    }

    private static async Task MoneyBoundaries(Fixture f)
    {
        var a = await f.Account(f.A, "Origem", -.005m); var b = await f.Account(f.A, "Destino", 0m);
        Check(a.OpeningBalance == -.01m && FinancialMoney.Round(.005m) == .01m, "Midpoints round away from zero symmetrically");
        foreach (var invalid in new[] { 0m, -.01m, .004m, 1_000_000_000_000m })
        {
            await Reject<ArgumentException>(() => f.Transaction(f.A, a.Id, invalid), "Invalid transaction amount was accepted");
            await Reject<ArgumentException>(() => f.Transfer(f.A, a.Id, b.Id, invalid), "Invalid transfer amount was accepted");
            await Reject<ArgumentException>(() => f.Commitment(f.A, invalid), "Invalid commitment amount was accepted");
        }
        await Reject<ArgumentException>(() => f.Account(f.A, "Limite", decimal.MinValue), "Account validation must reject extreme decimals without overflow");
        await Reject<ArgumentException>(() => Task.Run(() => new MonthlyBudget { Id = Guid.NewGuid() }.Update(.004m, 80)), "Budget cannot round to zero");
        await Reject<ArgumentException>(() => Task.Run(() => new FinancialGoal { Id = Guid.NewGuid() }.Update("Meta", FinancialGoalKind.Custom, .004m, Today, Today, null)), "Goal cannot round to zero");
    }

    private static async Task Dates(Fixture f)
    {
        f.Clock.Now = new DateTimeOffset(2026, 10, 1, 1, 30, 0, TimeSpan.Zero);
        var brazilDay = new DateOnly(2026, 9, 30);
        Check(FinancialCalendar.Today(f.Clock) == brazilDay, "UTC after midnight must remain previous Sao Paulo date");
        var account = await f.Account(f.A, "Principal", 0m);
        await f.Transaction(f.A, account.Id, 10m, date: brazilDay);
        var summary = await f.In(f.A, service => service.GetDashboardAsync(Ct));
        Check(summary.MonthIncome == 10m, "Month boundary must follow business timezone");
        var item = (await f.Commitment(f.A, 1m, due: brazilDay))[0];
        Check(!item.IsOverdue, "Due today must not be overdue because UTC moved ahead");
        await Reject<ArgumentException>(() => f.Transaction(f.A, account.Id, 1m, date: new(2026, 10, 1)), "Future actual movement must use commitments instead");
        await Reject<ArgumentException>(() => f.Transaction(f.A, account.Id, 1m, date: default), "Default date must be rejected");
    }

    private static async Task CashFlow(Fixture f)
    {
        var a = await f.Account(f.A, "Ativa", 100m); var b = await f.Account(f.A, "Arquivada", 50m);
        await f.Transfer(f.A, a.Id, b.Id, 20m); await f.Transaction(f.A, b.Id, 10m);
        await f.In(f.A, service => service.SetAccountActiveAsync(b.Id, false, Ct));
        var summary = await f.In(f.A, service => service.GetCashFlowAsync(30, Ct));
        var dashboard = await f.In(f.A, service => service.GetDashboardAsync(Ct));
        Check(summary.CurrentBalance == 80m && dashboard.TotalBalance == 80m && summary.Points.Single(item => item.Date == Today).Balance == 80m, "Active cash balance and chart must agree even with archived transfer destination");
        Check(summary.TotalIncome == 0m && summary.TotalExpense == 0m, "Inactive account movements and transfers cannot become cash-flow revenue");
        await Reject<KeyNotFoundException>(() => f.Transaction(f.A, b.Id, 1m), "Archived account must reject new movements");
    }

    private static async Task ImportFormats(Fixture f)
    {
        var account = await f.Account(f.A, "Banco", 0m);
        var csv = "\uFEFFData;Descrição;Valor\n2026-10-02;Entrada;1.234,56\n02/10/2026;Saída;-12.005\n02/10/2026;Americano;1,234.56\n20261002garbage;Inválida;2\n03/10/2026;Futura;2\n02/10/2026;Subcentavo;0.004\n31/02/2026;Inválida;2";
        var preview = await f.In(f.A, service => service.PreviewBankStatementAsync(account.Id, "bank.csv", csv, Ct));
        Check(preview.Ready == 3 && preview.Invalid == 4 && preview.TotalIncome == 2469.12m && preview.TotalExpense == 12.01m, "Import preview must normalize formats and match actual stored totals");
        var result = await f.In(f.A, service => service.ImportBankStatementAsync(account.Id, "bank.csv", csv, Ct));
        Check(result.Imported == 3 && result.Invalid == 4, "Only validated rows are imported");
        var ofx = "<OFX><STMTTRN><DTPOSTED>20261002120000[-3:BRT]<TRNAMT>-10.005<FITID>ofx-1<NAME>Pagamento</STMTTRN><STMTTRN><DTPOSTED>20260230120000<TRNAMT>1<FITID>ofx-2</STMTTRN></OFX>";
        var ofxResult = await f.In(f.A, service => service.ImportBankStatementAsync(account.Id, "bank.ofx", ofx, Ct));
        Check(ofxResult.Imported == 1 && ofxResult.Invalid == 1, "OFX timestamps and invalid calendar dates must be validated");
        var rows = (await f.In(f.A, service => service.GetReconciliationAsync(account.Id, Ct))).Entries;
        Check(rows.Any(item => item.SignedAmount == -10.01m), "OFX amount must follow same rounding as manual movement");
    }

    private static async Task ImportDuplicates(Fixture f)
    {
        var a = await f.Account(f.A, "Banco A", 0m); var b = await f.Account(f.B, "Banco B", 0m);
        var csv = "Data;Descrição;Valor\n02/10/2026;Receita;1,23\n02/10/2026;Receita;1,23";
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            var first = await f.In(f.A, service => service.ImportBankStatementAsync(a.Id, "bank.csv", csv, Ct));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var second = await f.In(f.A, service => service.ImportBankStatementAsync(a.Id, "bank.csv", csv, Ct));
            var other = await f.In(f.B, service => service.ImportBankStatementAsync(b.Id, "bank.csv", csv, Ct));
            Check(first.Imported == 1 && first.Duplicates == 1 && second.Imported == 0 && second.Duplicates == 2 && other.Imported == 1, "Duplicate detection must work within file, across locale and only within target account");
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    private static async Task Reconciliation(Fixture f)
    {
        var a = await f.Account(f.A, "Banco", 0m); var b = await f.Account(f.A, "Outro", 0m);
        var movement = await f.Transaction(f.A, a.Id, 10m);
        await f.Import(f.A, a.Id, "match", 10m); await f.Import(f.A, a.Id, "other", 10m);
        var rows = (await f.In(f.A, service => service.GetReconciliationAsync(a.Id, Ct))).Entries;
        await f.In(f.A, service => service.MatchBankEntryAsync(rows[0].Id, movement.Id, Ct));
        Check(await f.Balance(f.A, a.Id) == 10m, "Matching must not add another financial movement");
        await Reject<ArgumentException>(() => f.In(f.A, service => service.MatchBankEntryAsync(rows[1].Id, movement.Id, Ct)), "A movement cannot match two statement rows");
        await Reject<ArgumentException>(() => f.In(f.A, service => service.UpdateTransactionAsync(movement.Id, new(a.Id, "Editada", "Serviços", TransactionType.Income, 11m, Today), Ct)), "Matched movement cannot diverge from statement amount");
        var wrongAccount = await f.Transaction(f.A, b.Id, 10m);
        await Reject<KeyNotFoundException>(() => f.In(f.A, service => service.MatchBankEntryAsync(rows[1].Id, wrongAccount.Id, Ct)), "Reconciliation must use the same account");
    }

    private static async Task UndoReconciliation(Fixture f)
    {
        var a = await f.Account(f.A, "Banco", 0m);
        await f.Import(f.A, a.Id, "auto", 10m);
        var row = (await f.In(f.A, service => service.GetReconciliationAsync(a.Id, Ct))).Entries.Single();
        await f.In(f.A, service => service.CreateFromBankEntryAsync(row.Id, new("Serviços"), Ct));
        Check(await f.Balance(f.A, a.Id) == 10m, "Automatic reconciliation creates exactly one movement");
        await f.In(f.A, service => service.UndoReconciliationAsync(row.Id, false, Ct));
        var movement = (await f.In(f.A, service => service.GetTransactionsAsync(Ct))).Single();
        Check(await f.Balance(f.A, a.Id) == 10m, "Undo and keep must preserve cash");
        await f.In(f.A, service => service.MatchBankEntryAsync(row.Id, movement.Id, Ct));
        await Reject<InvalidOperationException>(() => f.In(f.A, service => service.UndoReconciliationAsync(row.Id, true, Ct)), "Matched existing movement must not be removed by automatic undo");
        await f.In(f.A, service => service.UndoReconciliationAsync(row.Id, false, Ct));
        await f.In(f.A, service => service.DeleteTransactionAsync(movement.Id, Ct));
        await f.In(f.A, service => service.CreateFromBankEntryAsync(row.Id, new("Serviços"), Ct));
        await f.In(f.A, service => service.UndoReconciliationAsync(row.Id, true, Ct));
        Check(await f.Balance(f.A, a.Id) == 0m && (await f.In(f.A, service => service.GetReconciliationAsync(a.Id, Ct))).Pending == 1, "Undo automatic creation reverses cash and reopens bank entry");
    }

    private static async Task VoidReconciliation(Fixture f)
    {
        var a = await f.Account(f.A, "Banco", 0m); var movement = await f.Transaction(f.A, a.Id, 10m);
        await f.Import(f.A, a.Id, "void", 10m);
        var row = (await f.In(f.A, service => service.GetReconciliationAsync(a.Id, Ct))).Entries.Single();
        await f.In(f.A, service => service.MatchBankEntryAsync(row.Id, movement.Id, Ct));
        await f.In(f.A, service => service.DeleteTransactionAsync(movement.Id, Ct));
        var current = (await f.In(f.A, service => service.GetReconciliationAsync(a.Id, Ct))).Entries.Single();
        Check(current.Status == "Pending" && current.TransactionId == null && await f.Balance(f.A, a.Id) == 0m, "Voided movement cannot remain reconciled");
        Check((await f.In(f.A, service => service.GetReconciliationHistoryAsync(a.Id, Ct))).Any(item => item.Action == "TransactionVoided"), "Reconciliation reopening records user and reason");
    }

    private static async Task AtomicImport(Fixture f)
    {
        var a = await f.Account(f.A, "Banco", 0m);
        await f.In(f.A, service => service.CreateCategoryAsync(new("Serviços", CategoryKind.Income, null), Ct));
        var csv = "Data;Descrição;Valor;Id\n02/10/2026;Receita A;10;a\n02/10/2026;Receita B;20;b";
        await Reject<ArgumentException>(() => f.In(f.A, service => service.ConfirmBankStatementAsync(a.Id, "bank.csv", csv,
            [new(2, "create", null, "Serviços", null, false, null), new(3, "create", null, "Categoria inexistente", null, false, null)], Ct)), "Invalid decision must reject whole batch");
        using var db = f.Db();
        Check(await db.BankStatementEntries.CountAsync(Ct) == 0 && await db.Transactions.CountAsync(Ct) == 0, "Import failure cannot leave any bank rows or movements");
        var accepted = await f.In(f.A, service => service.ConfirmBankStatementAsync(a.Id, "bank.csv", csv,
            [new(2, "create", null, "Serviços", null, false, null), new(3, "create", null, "Serviços", null, false, null)], Ct));
        Check(accepted.Created == 2 && await f.Balance(f.A, a.Id) == 30m, "Corrected import commits the complete reviewed batch");
    }

    private static async Task Isolation(Fixture f)
    {
        var a = await f.Account(f.A, "Conta A", 100m); var b = await f.Account(f.B, "Conta B", 999m);
        var foreign = await f.Transaction(f.B, b.Id, 1m); var debt = (await f.Commitment(f.B, 10m))[0];
        Check((await f.In(f.A, service => service.GetAccountsAsync(Ct))).All(item => item.Id != b.Id), "Account listing must not expose other spaces");
        Check((await f.In(f.A, service => service.GetTransactionsAsync(Ct))).Count == 0 && (await f.In(f.A, service => service.GetCommitmentsAsync(Ct))).Count == 0, "Movement and commitment listings remain scoped");
        Check((await f.In(f.A, service => service.GetWorkspacesAsync(Ct))).Select(item => item.Id).SequenceEqual(new[] { f.A }), "Only memberships grant workspace listing");
        await Reject<KeyNotFoundException>(() => f.Transaction(f.A, b.Id, 1m), "Foreign account cannot receive movements");
        await Reject<KeyNotFoundException>(() => f.Transfer(f.A, a.Id, b.Id, 1m), "Transfer cannot cross spaces");
        await Reject<KeyNotFoundException>(() => f.Settle(f.A, debt.Id, a.Id, 1m), "Foreign commitment cannot be settled");
        Check(!await f.In(f.A, service => service.DeleteTransactionAsync(foreign.Id, Ct)), "Foreign movement cannot be voided");
        await Reject<KeyNotFoundException>(() => f.In(f.A, service => service.UpdateTransactionAsync(foreign.Id, new(a.Id, "Ataque", "Serviços", TransactionType.Income, 100m, Today), Ct)), "Foreign movement cannot be reassigned");
        await Reject<KeyNotFoundException>(() => f.In(f.A, service => service.ImportBankStatementAsync(b.Id, "bank.csv", "Data;Descrição;Valor\n02/10/2026;Ataque;10", Ct)), "Import cannot target foreign account");
        Check(await f.Balance(f.B, b.Id) == 1000m, "Rejected foreign operations preserve original balance");
    }

    private static async Task Allocations(Fixture f)
    {
        var a = await f.Account(f.A, "Principal", 0m); var transaction = await f.Transaction(f.A, a.Id, .01m);
        var one = await f.In(f.A, service => service.CreateCostCenterAsync(new("Centro A", "CA"), Ct));
        var two = await f.In(f.A, service => service.CreateCostCenterAsync(new("Centro B", "CB"), Ct));
        await Reject<ArgumentException>(() => f.In(f.A, service => service.ReplaceAllocationsAsync(transaction.Id, [new(one.Id, .005m), new(two.Id, .005m)], Ct)), "Rounded allocations cannot sum to twice the transaction");
        var allocation = await f.In(f.A, service => service.ReplaceAllocationsAsync(transaction.Id, [new(one.Id, .01m)], Ct));
        Check(allocation.Sum(item => item.Amount) == .01m, "Saved allocations must sum to transaction cents");
        await Reject<ArgumentException>(() => f.In(f.A, service => service.UpdateTransactionAsync(transaction.Id, new(a.Id, "Editada", "Serviços", TransactionType.Income, .02m, Today), Ct)), "Changing amount cannot invalidate existing allocations");
    }

    private static async Task LegacyMigration()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True"); await connection.OpenAsync(Ct);
        await using var db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(connection).Options);
        var previous = db.Database.GetMigrations().Single(name => name.EndsWith("_EmailOutbox"));
        await db.GetService<IMigrator>().MigrateAsync(previous, Ct);
        var workspace = Guid.NewGuid(); var user = Guid.NewGuid(); var account = Guid.NewGuid(); var debt = Guid.NewGuid(); var payment = Guid.NewGuid();
        db.Workspaces.Add(new Workspace { Id = workspace, Name = "Legacy", Kind = WorkspaceKind.Business, CreatedAt = DateTimeOffset.UtcNow });
        db.Users.Add(new ApplicationUser { Id = user, Name = "Legacy", Email = "legacy@example.com", PasswordHash = "test-only", CreatedAt = DateTimeOffset.UtcNow });
        db.Accounts.Add(new FinancialAccount { Id = account, WorkspaceId = workspace, Name = "Conta", Institution = "Banco", OpeningBalance = 100m });
        var commitment = new FinancialCommitment { Id = debt, WorkspaceId = workspace, SeriesId = Guid.NewGuid(), Description = "Conta", Contact = "Fornecedor", Category = "Serviços", Kind = CommitmentKind.Payable, TotalAmount = 25m, DueDate = Today, CompetenceDate = Today, CreatedAt = DateTimeOffset.UtcNow };
        commitment.Settle(25m); db.Commitments.Add(commitment);
        db.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), WorkspaceId = workspace, UserId = user, EntityId = debt, EntityType = "Commitment", Action = "Settled", Description = "Baixa", Details = JsonSerializer.Serialize(new { TransactionId = payment }), OccurredAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Transactions (Id, WorkspaceId, AccountId, Description, Category, Type, Amount, OccurredOn, CreatedAt, IsVoided) VALUES ({payment}, {workspace}, {account}, {"Conta"}, {"Serviços"}, {1}, {25m}, {Today}, {DateTimeOffset.UtcNow}, {false})", Ct);
        await db.Database.MigrateAsync(Ct);
        Check((await db.Transactions.AsNoTracking().SingleAsync(Ct)).CommitmentId == debt, "Migration must reconstruct old payment link from audit safely");
        db.ChangeTracker.Clear();
        await new SqliteFinanceRepository(db).VoidTransactionAsync(workspace, payment, user, DateTimeOffset.UtcNow, Ct);
        db.ChangeTracker.Clear();
        Check((await db.Commitments.SingleAsync(Ct)).Status == CommitmentStatus.Open, "Historical linked payment can be reversed after migration");
    }

    private static async Task HttpIsolation()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
        var api = Path.Combine(AppContext.BaseDirectory, "Financeiro.Api.dll");
        var contentRoot = Path.Combine(projectRoot, "backend/tests/Financeiro.Operations.Tests/TestData/http-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(api); start.ArgumentList.Add("--contentRoot"); start.ArgumentList.Add(contentRoot);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        start.Environment["Backups__Enabled"] = "false"; start.Environment["Email__Enabled"] = "false";
        start.Environment["Email__PublicUrl"] = "http://localhost:5173"; start.Environment["Database__File"] = "App_Data/finance.db";
        using var process = Process.Start(start) ?? throw new Exception("Cannot start isolated API");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        HttpClient Client() => new(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            using var anonymous = Client();
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (process.HasExited) throw new Exception("API failed to start: " + await errors);
                try { if ((await anonymous.GetAsync("/api/health", timeout.Token)).IsSuccessStatusCode) break; } catch (HttpRequestException) { }
                await Task.Delay(150, timeout.Token);
            }
            Check((await anonymous.GetAsync("/api/accounts", timeout.Token)).StatusCode == HttpStatusCode.Unauthorized, "Anonymous client cannot read finances");
            using var owner = Client(); using var outsider = Client();
            var ownerRegistration = await owner.PostAsJsonAsync("/api/auth/register", new { name = "Owner Test", email = "owner@example.com", password = "OwnerPassword123" }, timeout.Token);
            var outsiderRegistration = await outsider.PostAsJsonAsync("/api/auth/register", new { name = "Outsider Test", email = "outsider@example.com", password = "OutsiderPassword123" }, timeout.Token);
            Check(ownerRegistration.IsSuccessStatusCode && outsiderRegistration.IsSuccessStatusCode, "Test accounts register through HTTP");
            var ownerSpaces = await owner.GetFromJsonAsync<JsonElement>("/api/workspaces", timeout.Token);
            var outsiderSpaces = await outsider.GetFromJsonAsync<JsonElement>("/api/workspaces", timeout.Token);
            var ownerSpace = ownerSpaces[0].GetProperty("id").GetString()!; var outsiderSpace = outsiderSpaces[0].GetProperty("id").GetString()!;
            owner.DefaultRequestHeaders.Add("X-Workspace-Id", ownerSpace); outsider.DefaultRequestHeaders.Add("X-Workspace-Id", ownerSpace);
            Check((await outsider.GetAsync("/api/accounts", timeout.Token)).StatusCode == HttpStatusCode.Forbidden, "Authenticated outsider cannot select another workspace by header");
            Check((await outsider.PostAsJsonAsync("/api/accounts", new { name = "Intrusa", institution = "Banco", type = "Checking", openingBalance = 999 }, timeout.Token)).StatusCode == HttpStatusCode.Forbidden, "Outsider cannot write to other workspace");
            outsider.DefaultRequestHeaders.Remove("X-Workspace-Id"); outsider.DefaultRequestHeaders.Add("X-Workspace-Id", "invalid-guid");
            Check((await outsider.GetAsync("/api/accounts", timeout.Token)).StatusCode == HttpStatusCode.BadRequest, "Malformed workspace header cannot fall back silently");
            outsider.DefaultRequestHeaders.Remove("X-Workspace-Id"); outsider.DefaultRequestHeaders.Add("X-Workspace-Id", outsiderSpace);
            Check((await outsider.GetAsync("/api/accounts", timeout.Token)).IsSuccessStatusCode, "Outsider retains access to their own workspace");
            var invitation = await owner.PostAsJsonAsync($"/api/workspaces/{ownerSpace}/invitations", new { email = "outsider@example.com", role = "Member" }, timeout.Token);
            Check(invitation.IsSuccessStatusCode, "Owner can invite member");
            var invitationId = (await invitation.Content.ReadFromJsonAsync<JsonElement>(timeout.Token)).GetProperty("id").GetString();
            Check((await outsider.PostAsync($"/api/workspace-invitations/{invitationId}/accept", null, timeout.Token)).IsSuccessStatusCode, "Matching user can accept invitation");
            outsider.DefaultRequestHeaders.Remove("X-Workspace-Id"); outsider.DefaultRequestHeaders.Add("X-Workspace-Id", ownerSpace);
            Check((await outsider.GetAsync("/api/accounts", timeout.Token)).IsSuccessStatusCode, "Read-only member can consult financial data");
            Check((await outsider.PostAsJsonAsync("/api/accounts", new { name = "Proibida", institution = "Banco", type = "Checking", openingBalance = 1 }, timeout.Token)).StatusCode == HttpStatusCode.Forbidden, "Read-only member cannot mutate financial data");
            Check((await outsider.GetAsync("/api/platform/backups", timeout.Token)).StatusCode == HttpStatusCode.Forbidden, "Members cannot download global backups");
        }
        finally
        {
            if (!process.HasExited) process.Kill(true);
            await process.WaitForExitAsync(); await output; await errors;
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 2, 15, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed record Context(Guid WorkspaceId, Guid UserId) : IWorkspaceContext, IUserContext;
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");
        public Guid A { get; } = Guid.NewGuid(); public Guid B { get; } = Guid.NewGuid();
        private Guid UserA { get; } = Guid.NewGuid(); private Guid UserB { get; } = Guid.NewGuid();
        public FixedClock Clock { get; } = new();
        public FinanceDbContext Db() => new(new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(connection).Options);
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture(); await f.connection.OpenAsync(Ct);
            using var db = f.Db(); await db.Database.EnsureCreatedAsync(Ct);
            foreach (var pair in new[] { (f.A, f.UserA), (f.B, f.UserB) })
            {
                db.Workspaces.Add(new Workspace { Id = pair.Item1, Name = pair.Item1.ToString(), Kind = WorkspaceKind.Business, CreatedAt = DateTimeOffset.UtcNow });
                db.Users.Add(new ApplicationUser { Id = pair.Item2, Name = "Test", Email = pair.Item2 + "@example.com", PasswordHash = "test-only", CreatedAt = DateTimeOffset.UtcNow });
                db.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = pair.Item1, UserId = pair.Item2, Role = WorkspaceRole.Owner, JoinedAt = DateTimeOffset.UtcNow });
            }
            await db.SaveChangesAsync(Ct); return f;
        }
        public async Task<T> In<T>(Guid workspace, Func<FinanceService, Task<T>> action)
        {
            using var db = Db(); var context = new Context(workspace, workspace == A ? UserA : UserB);
            return await action(new FinanceService(new SqliteFinanceRepository(db), context, context, Clock));
        }
        public Task<AccountView> Account(Guid workspace, string name, decimal opening) => In(workspace, service => service.CreateAccountAsync(new(name, "Banco", null, AccountType.Checking, opening), Ct));
        public Task<TransactionView> Transaction(Guid workspace, Guid account, decimal amount, TransactionType type = TransactionType.Income, DateOnly? date = null)
            => In(workspace, service => service.CreateTransactionAsync(new(account, "Lançamento", "Serviços", type, amount, date ?? Today), Ct));
        public Task<IReadOnlyList<TransactionView>> Transfer(Guid workspace, Guid source, Guid destination, decimal amount)
            => In(workspace, service => service.CreateTransferAsync(new(source, destination, "Transferência", amount, Today), Ct));
        public Task<IReadOnlyList<CommitmentView>> Commitment(Guid workspace, decimal amount, int installments = 1, RecurrenceFrequency? frequency = null, int count = 1, DateOnly? due = null, CommitmentKind kind = CommitmentKind.Payable)
            => In(workspace, service => service.CreateCommitmentsAsync(new(null, "Compromisso", "Fornecedor", "Serviços", kind, amount, due ?? Today, due ?? Today, installments, frequency, count), Ct));
        public Task<CommitmentView> Settle(Guid workspace, Guid commitment, Guid account, decimal amount)
            => In(workspace, service => service.SettleCommitmentAsync(commitment, new(account, amount, Today), Ct));
        public async Task<decimal> Balance(Guid workspace, Guid account)
            => (await In(workspace, service => service.GetAccountsAsync(Ct, true))).Single(item => item.Id == account).Balance;
        public Task<BankImportResult> Import(Guid workspace, Guid account, string id, decimal amount)
            => In(workspace, service => service.ImportBankStatementAsync(account, "bank.csv", $"Data;Descrição;Valor;Id\n02/10/2026;Lançamento;{amount.ToString(CultureInfo.InvariantCulture)};{id}", Ct));
        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
