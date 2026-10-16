using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Financeiro.Domain;

namespace Financeiro.Application;

public sealed class FinanceService(IFinanceRepository repository, IWorkspaceContext workspaceContext, IUserContext userContext, TimeProvider? timeProvider = null)
{
    public static readonly Guid SeedWorkspaceId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private Guid CurrentWorkspaceId => workspaceContext.WorkspaceId;
    private DateOnly Today => FinancialCalendar.Today(timeProvider ?? TimeProvider.System);

    public async Task<IReadOnlyList<WorkspaceView>> GetWorkspacesAsync(CancellationToken ct)
    {
        var memberships = await repository.GetWorkspaceMembershipsAsync(userContext.UserId, ct);
        var roles = memberships.ToDictionary(member => member.WorkspaceId, member => member.Role);
        return (await repository.GetWorkspacesAsync(ct)).Where(workspace => roles.ContainsKey(workspace.Id)).Select(workspace => ToView(workspace, roles[workspace.Id])).ToList();
    }

    public async Task<WorkspaceView> CreateWorkspaceAsync(SaveWorkspaceCommand command, CancellationToken ct)
    {
        ValidateWorkspace(command);
        var workspaces = (await GetWorkspacesAsync(ct)).ToList();
        if (workspaces.Any(workspace => workspace.Name.Equals(command.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Já existe um espaço com esse nome.");
        var workspace = new Workspace { Id = Guid.NewGuid(), Name = command.Name.Trim(), Kind = command.Kind, CreatedAt = DateTimeOffset.UtcNow };
        var owner = new WorkspaceMember { WorkspaceId = workspace.Id, UserId = userContext.UserId, Role = WorkspaceRole.Owner, JoinedAt = DateTimeOffset.UtcNow };
        await repository.SaveWorkspaceWithOwnerAsync(workspace, owner, ct);
        return ToView(workspace, owner.Role);
    }

    public async Task<WorkspaceView> UpdateWorkspaceAsync(Guid workspaceId, SaveWorkspaceCommand command, CancellationToken ct)
    {
        ValidateWorkspace(command);
        var memberships = await repository.GetWorkspaceMembershipsAsync(userContext.UserId, ct);
        var membership = memberships.FirstOrDefault(item => item.WorkspaceId == workspaceId) ?? throw new KeyNotFoundException("Espaço não encontrado.");
        if (membership.Role is not (WorkspaceRole.Owner or WorkspaceRole.Administrator)) throw new UnauthorizedAccessException("Seu perfil não permite editar este espaço.");
        var workspaces = await repository.GetWorkspacesAsync(ct);
        var workspace = workspaces.FirstOrDefault(item => item.Id == workspaceId) ?? throw new KeyNotFoundException("Espaço não encontrado.");
        var accessibleIds = memberships.Select(item => item.WorkspaceId).ToHashSet();
        if (workspaces.Any(item => accessibleIds.Contains(item.Id) && item.Id != workspaceId && item.Name.Equals(command.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Já existe um espaço com esse nome.");
        workspace.Update(command.Name.Trim(), command.Kind);
        await repository.SaveWorkspaceAsync(workspace, false, ct);
        return ToView(workspace, membership.Role);
    }

    public async Task<IReadOnlyList<AccountView>> GetAccountsAsync(CancellationToken ct, bool includeInactive = false)
    {
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        return accounts.Where(account => includeInactive || account.IsActive).Select(account => ToView(account, transactions)).ToList();
    }

    public async Task<AccountView> CreateAccountAsync(SaveAccountCommand command, CancellationToken ct)
    {
        ValidateAccount(command);
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        if (accounts.Any(account => account.Name.Equals(command.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Já existe uma conta com esse nome.");
        var account = new FinancialAccount
        {
            Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, Name = command.Name.Trim(),
            Institution = command.Institution.Trim(), LastFourDigits = NormalizeDigits(command.LastFourDigits),
            Type = command.Type, OpeningBalance = FinancialMoney.Round(command.OpeningBalance)
        };
        await repository.SaveAccountAsync(account, true, ct);
        return ToView(account, []);
    }

    public async Task<AccountView> UpdateAccountAsync(Guid accountId, SaveAccountCommand command, CancellationToken ct)
    {
        ValidateAccount(command);
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        var account = accounts.FirstOrDefault(item => item.Id == accountId) ?? throw new KeyNotFoundException("Conta não encontrada.");
        if (accounts.Any(item => item.Id != accountId && item.Name.Equals(command.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Já existe uma conta com esse nome.");
        account.Update(command.Name.Trim(), command.Institution.Trim(), NormalizeDigits(command.LastFourDigits), command.Type, FinancialMoney.Round(command.OpeningBalance));
        await repository.SaveAccountAsync(account, false, ct);
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        return ToView(account, transactions);
    }

    public async Task<AccountView> SetAccountActiveAsync(Guid accountId, bool active, CancellationToken ct)
    {
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        var account = accounts.FirstOrDefault(item => item.Id == accountId) ?? throw new KeyNotFoundException("Conta não encontrada.");
        if (!active && account.IsActive && accounts.Count(item => item.IsActive) == 1)
            throw new ArgumentException("Mantenha pelo menos uma conta ativa.");
        account.SetActive(active);
        await repository.SaveAccountAsync(account, false, ct);
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        return ToView(account, transactions);
    }

    public async Task<IReadOnlyList<TransactionView>> GetTransactionsAsync(CancellationToken ct)
    {
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        return transactions.OrderByDescending(t => t.OccurredOn).ThenByDescending(t => t.CreatedAt).Select(t => ToView(t, accounts)).ToList();
    }

    public async Task<DashboardSummary> GetDashboardAsync(CancellationToken ct)
    {
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        var today = Today;
        var month = transactions.Where(t => !t.IsTransfer && t.OccurredOn <= today && t.OccurredOn.Year == today.Year && t.OccurredOn.Month == today.Month).ToList();
        var income = month.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var expense = month.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
        var accountViews = accounts.Where(a => a.IsActive).Select(a => ToView(a, transactions)).ToList();
        var recent = transactions.OrderByDescending(t => t.OccurredOn).ThenByDescending(t => t.CreatedAt).Take(5).Select(t => ToView(t, accounts)).ToList();
        return new DashboardSummary(accountViews.Sum(a => a.Balance), income, expense, income - expense, accountViews, recent);
    }

    public async Task<TransactionView> CreateTransactionAsync(CreateTransactionCommand command, CancellationToken ct)
    {
        ValidateMovementDate(command.OccurredOn);
        if (!Enum.IsDefined(command.Type)) throw new ArgumentException("Tipo de movimentação inválido.");
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        if (!accounts.Any(a => a.Id == command.AccountId && a.IsActive)) throw new KeyNotFoundException("Conta não encontrada.");
        var rule = await MatchCategorizationRuleAsync(command.AccountId, command.Description, command.Type, ct);
        var category = rule?.Category ?? command.Category;
        ValidateTextAndAmount(command.Description, category, command.Amount);
        var transaction = NewTransaction(command.AccountId, command.Description.Trim(), category.Trim(), command.Type, command.Amount, command.OccurredOn);
        await repository.AddTransactionAsync(transaction, ct);
        if (rule?.CostCenterId is Guid costCenterId) await repository.ReplaceAllocationsAsync(CurrentWorkspaceId, transaction.Id, [new TransactionAllocation { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, TransactionId = transaction.Id, CostCenterId = costCenterId, Amount = transaction.Amount }], ct);
        await AuditAsync("Created", "Transaction", transaction.Id, $"Lançamento criado: {transaction.Description}", new { transaction.AccountId, transaction.Type, transaction.Amount, transaction.OccurredOn }, ct);
        return ToView(transaction, accounts);
    }

    public async Task<TransactionView> UpdateTransactionAsync(Guid transactionId, CreateTransactionCommand command, CancellationToken ct)
    {
        ValidateMovementDate(command.OccurredOn);
        if (!Enum.IsDefined(command.Type)) throw new ArgumentException("Tipo de movimentação inválido.");
        ValidateTextAndAmount(command.Description, command.Category, command.Amount);
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        if (!accounts.Any(account => account.Id == command.AccountId && account.IsActive)) throw new KeyNotFoundException("Conta não encontrada.");
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        var transaction = transactions.FirstOrDefault(item => item.Id == transactionId) ?? throw new KeyNotFoundException("Lançamento não encontrado.");
        if (transaction.IsTransfer) throw new ArgumentException("Edite esta movimentação como transferência.");
        if (transaction.CommitmentId.HasValue) throw new ArgumentException("Estorne a baixa e registre uma nova baixa para alterar esta movimentação.");
        if ((await repository.GetBankStatementEntriesAsync(CurrentWorkspaceId, ct)).Any(item => item.TransactionId == transaction.Id))
            throw new ArgumentException("Desfaça a conciliação antes de editar esta movimentação.");
        if ((await repository.GetAllocationsAsync(CurrentWorkspaceId, ct)).Any(item => item.TransactionId == transaction.Id) && FinancialMoney.Round(command.Amount) != transaction.Amount)
            throw new ArgumentException("Remova o rateio antes de alterar o valor da movimentação.");
        transaction.Update(command.AccountId, command.Description.Trim(), command.Category.Trim(), command.Type, FinancialMoney.Round(command.Amount), command.OccurredOn);
        await repository.UpdateTransactionsAsync([transaction], ct);
        await AuditAsync("Updated", "Transaction", transaction.Id, $"Lançamento editado: {transaction.Description}", new { transaction.AccountId, transaction.Type, transaction.Amount, transaction.OccurredOn }, ct);
        return ToView(transaction, accounts);
    }

    public async Task<IReadOnlyList<TransactionView>> CreateTransferAsync(CreateTransferCommand command, CancellationToken ct)
    {
        ValidateMovementDate(command.OccurredOn);
        FinancialMoney.Positive(command.Amount);
        if (command.SourceAccountId == command.DestinationAccountId) throw new ArgumentException("Escolha contas diferentes para a transferência.");
        var description = command.Description.Trim();
        if (description.Length is < 2 or > 120) throw new ArgumentException("A descrição deve ter entre 2 e 120 caracteres.");
        if (command.Amount <= 0 || command.Amount > 999_999_999_999m) throw new ArgumentException("Informe um valor de transferência válido.");
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        var source = accounts.FirstOrDefault(account => account.Id == command.SourceAccountId && account.IsActive) ?? throw new KeyNotFoundException("Conta de origem não encontrada.");
        var destination = accounts.FirstOrDefault(account => account.Id == command.DestinationAccountId && account.IsActive) ?? throw new KeyNotFoundException("Conta de destino não encontrada.");
        var transferId = Guid.NewGuid();
        var amount = FinancialMoney.Round(command.Amount);
        var createdAt = DateTimeOffset.UtcNow;
        var transactions = new[]
        {
            new FinancialTransaction { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, AccountId = source.Id, CounterpartyAccountId = destination.Id, TransferId = transferId, Description = description, Category = "Transferência", Type = TransactionType.Expense, Amount = amount, OccurredOn = command.OccurredOn, CreatedAt = createdAt },
            new FinancialTransaction { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, AccountId = destination.Id, CounterpartyAccountId = source.Id, TransferId = transferId, Description = description, Category = "Transferência", Type = TransactionType.Income, Amount = amount, OccurredOn = command.OccurredOn, CreatedAt = createdAt }
        };
        await repository.AddTransactionsAsync(transactions, ct);
        await AuditAsync("Created", "Transfer", transferId, $"Transferência criada: {description}", new { SourceAccountId = source.Id, DestinationAccountId = destination.Id, Amount = amount, command.OccurredOn }, ct);
        return transactions.Select(transaction => ToView(transaction, accounts)).ToList();
    }

    public async Task<IReadOnlyList<TransactionView>> UpdateTransferAsync(Guid transferId, CreateTransferCommand command, CancellationToken ct)
    {
        ValidateMovementDate(command.OccurredOn);
        FinancialMoney.Positive(command.Amount);
        if (command.SourceAccountId == command.DestinationAccountId) throw new ArgumentException("Escolha contas diferentes para a transferência.");
        var description = command.Description.Trim();
        if (description.Length is < 2 or > 120) throw new ArgumentException("A descrição deve ter entre 2 e 120 caracteres.");
        if (command.Amount <= 0 || command.Amount > 999_999_999_999m) throw new ArgumentException("Informe um valor de transferência válido.");
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        var source = accounts.FirstOrDefault(account => account.Id == command.SourceAccountId && account.IsActive) ?? throw new KeyNotFoundException("Conta de origem não encontrada.");
        var destination = accounts.FirstOrDefault(account => account.Id == command.DestinationAccountId && account.IsActive) ?? throw new KeyNotFoundException("Conta de destino não encontrada.");
        var transactions = (await repository.GetTransactionsAsync(CurrentWorkspaceId, ct)).Where(item => item.TransferId == transferId).ToList();
        if (transactions.Count != 2) throw new KeyNotFoundException("Transferência não encontrada.");
        var entries = await repository.GetBankStatementEntriesAsync(CurrentWorkspaceId, ct);
        if (entries.Any(item => transactions.Any(transaction => transaction.Id == item.TransactionId)))
            throw new ArgumentException("Desfaça a conciliação antes de editar esta transferência.");
        var expense = transactions.FirstOrDefault(item => item.Type == TransactionType.Expense) ?? throw new InvalidOperationException("Transferência inconsistente.");
        var income = transactions.FirstOrDefault(item => item.Type == TransactionType.Income) ?? throw new InvalidOperationException("Transferência inconsistente.");
        var amount = FinancialMoney.Round(command.Amount);
        expense.Update(source.Id, description, "Transferência", TransactionType.Expense, amount, command.OccurredOn, destination.Id);
        income.Update(destination.Id, description, "Transferência", TransactionType.Income, amount, command.OccurredOn, source.Id);
        await repository.UpdateTransactionsAsync(transactions, ct);
        await AuditAsync("Updated", "Transfer", transferId, $"Transferência editada: {description}", new { SourceAccountId = source.Id, DestinationAccountId = destination.Id, Amount = amount, command.OccurredOn }, ct);
        return transactions.Select(transaction => ToView(transaction, accounts)).ToList();
    }

    public async Task<IReadOnlyList<CommitmentView>> GetCommitmentsAsync(CancellationToken ct)
    {
        var commitments = await repository.GetCommitmentsAsync(CurrentWorkspaceId, ct);
        return commitments.OrderBy(c => c.DueDate).ThenBy(c => c.Description).Select(ToView).ToList();
    }

    public async Task<IReadOnlyList<CommitmentView>> CreateCommitmentsAsync(CreateCommitmentCommand command, CancellationToken ct)
    {
        ValidateFinancialDate(command.DueDate); ValidateFinancialDate(command.CompetenceDate);
        if (!Enum.IsDefined(command.Kind) || (command.RecurrenceFrequency.HasValue && !Enum.IsDefined(command.RecurrenceFrequency.Value)))
            throw new ArgumentException("Tipo ou frequência do compromisso inválidos.");
        ValidateTextAndAmount(command.Description, command.Category, command.TotalAmount);
        var contact = await ResolveContactAsync(command.ContactId, command.Contact, command.Kind, ct);
        if (contact.Length is < 2 or > 100) throw new ArgumentException("O contato deve ter entre 2 e 100 caracteres.");
        if (command.Installments is < 1 or > 48) throw new ArgumentException("O parcelamento deve ter entre 1 e 48 parcelas.");
        if (command.RecurrenceFrequency.HasValue && command.Installments != 1) throw new ArgumentException("Use parcelamento ou recorrência, não os dois ao mesmo tempo.");
        if (command.RecurrenceFrequency.HasValue && command.RecurrenceCount is < 2 or > 60) throw new ArgumentException("A recorrência deve gerar entre 2 e 60 lançamentos.");
        var seriesId = Guid.NewGuid();
        var count = command.RecurrenceFrequency.HasValue ? command.RecurrenceCount : command.Installments;
        var totalAmount = FinancialMoney.Positive(command.TotalAmount);
        var baseAmount = command.RecurrenceFrequency.HasValue ? totalAmount : decimal.Floor(totalAmount * 100 / command.Installments) / 100;
        if (baseAmount <= 0) throw new ArgumentException("Cada parcela deve ter pelo menos um centavo.");
        var remainder = command.RecurrenceFrequency.HasValue ? 0 : totalAmount - baseAmount * command.Installments;
        var items = Enumerable.Range(0, count).Select(index => new FinancialCommitment
        {
            Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, SeriesId = seriesId,
            ContactId = command.ContactId, Description = command.Description.Trim(), Contact = contact, Category = command.Category.Trim(), Kind = command.Kind,
            TotalAmount = baseAmount + (!command.RecurrenceFrequency.HasValue && index == command.Installments - 1 ? remainder : 0),
            DueDate = AddFrequency(command.DueDate, command.RecurrenceFrequency, index), CompetenceDate = AddFrequency(command.CompetenceDate, command.RecurrenceFrequency, index),
            InstallmentNumber = command.RecurrenceFrequency.HasValue ? 1 : index + 1, InstallmentCount = command.RecurrenceFrequency.HasValue ? 1 : command.Installments,
            RecurrenceFrequency = command.RecurrenceFrequency, RecurrenceNumber = command.RecurrenceFrequency.HasValue ? index + 1 : 1, RecurrenceCount = command.RecurrenceFrequency.HasValue ? count : 1,
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();
        await repository.AddCommitmentsAsync(items, ct);
        await AuditAsync("Created", "CommitmentSeries", seriesId, $"{items.Count} compromisso(s) criado(s): {command.Description.Trim()}", new { command.Kind, command.TotalAmount, command.Installments, command.RecurrenceFrequency, command.RecurrenceCount }, ct);
        return items.Select(ToView).ToList();
    }

    public async Task<CommitmentView> SettleCommitmentAsync(Guid commitmentId, SettleCommitmentCommand command, CancellationToken ct)
    {
        ValidateMovementDate(command.OccurredOn);
        var amount = FinancialMoney.Positive(command.Amount);
        var commitments = await repository.GetCommitmentsAsync(CurrentWorkspaceId, ct);
        var commitment = commitments.FirstOrDefault(c => c.Id == commitmentId) ?? throw new KeyNotFoundException("Compromisso não encontrado.");
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        if (!accounts.Any(a => a.Id == command.AccountId && a.IsActive)) throw new KeyNotFoundException("Conta não encontrada.");
        commitment.Settle(amount);
        var type = commitment.Kind == CommitmentKind.Receivable ? TransactionType.Income : TransactionType.Expense;
        var transaction = NewTransaction(command.AccountId, commitment.Description, commitment.Category, type, amount, command.OccurredOn, commitment.Id);
        commitment = await repository.SettleCommitmentAsync(commitment, transaction, ct);
        await AuditAsync("Settled", "Commitment", commitment.Id, $"Baixa registrada: {commitment.Description}", new { command.AccountId, command.Amount, command.OccurredOn, TransactionId = transaction.Id }, ct);
        return ToView(commitment);
    }

    public async Task<CommitmentView> CancelCommitmentAsync(Guid commitmentId, CancellationToken ct)
    {
        var commitments = await repository.GetCommitmentsAsync(CurrentWorkspaceId, ct);
        var commitment = commitments.FirstOrDefault(c => c.Id == commitmentId) ?? throw new KeyNotFoundException("Compromisso não encontrado.");
        commitment.Cancel();
        await repository.UpdateCommitmentAsync(commitment, ct);
        await AuditAsync("Cancelled", "Commitment", commitment.Id, $"Compromisso cancelado: {commitment.Description}", null, ct);
        return ToView(commitment);
    }

    public async Task<CommitmentView> UpdateCommitmentAsync(Guid commitmentId, UpdateCommitmentCommand command, CancellationToken ct)
    {
        ValidateFinancialDate(command.DueDate); ValidateFinancialDate(command.CompetenceDate);
        if (!Enum.IsDefined(command.Kind)) throw new ArgumentException("Tipo de compromisso inválido.");
        ValidateTextAndAmount(command.Description, command.Category, command.TotalAmount);
        var contact = await ResolveContactAsync(command.ContactId, command.Contact, command.Kind, ct);
        if (contact.Length is < 2 or > 100) throw new ArgumentException("O contato deve ter entre 2 e 100 caracteres.");
        var commitments = await repository.GetCommitmentsAsync(CurrentWorkspaceId, ct);
        var commitment = commitments.FirstOrDefault(item => item.Id == commitmentId) ?? throw new KeyNotFoundException("Compromisso não encontrado.");
        commitment.ContactId = command.ContactId;
        commitment.Update(command.Description.Trim(), contact, command.Category.Trim(), command.Kind, FinancialMoney.Positive(command.TotalAmount), command.DueDate, command.CompetenceDate);
        await repository.UpdateCommitmentAsync(commitment, ct);
        await AuditAsync("Updated", "Commitment", commitment.Id, $"Compromisso editado: {commitment.Description}", new { commitment.ContactId, commitment.Kind, commitment.TotalAmount, commitment.DueDate, commitment.CompetenceDate }, ct);
        return ToView(commitment);
    }

    public async Task<CashFlowSummary> GetCashFlowAsync(int days, CancellationToken ct)
    {
        if (days is not (30 or 60 or 90)) throw new ArgumentException("O período deve ser de 30, 60 ou 90 dias.");
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        var commitments = await repository.GetCommitmentsAsync(CurrentWorkspaceId, ct);
        var today = Today;
        var startDate = today.AddDays(-14);
        var endDate = today.AddDays(days);
        var activeAccountIds = accounts.Where(account => account.IsActive).Select(account => account.Id).ToHashSet();
        transactions = transactions.Where(transaction => activeAccountIds.Contains(transaction.AccountId)).ToList();
        var openingBalance = accounts.Where(a => a.IsActive).Sum(a => a.OpeningBalance) + transactions.Where(t => t.OccurredOn < startDate).Sum(t => t.SignedAmount);
        var currentBalance = accounts.Where(a => a.IsActive).Sum(a => a.OpeningBalance) + transactions.Where(t => t.OccurredOn <= today).Sum(t => t.SignedAmount);
        var periodTransactions = transactions.Where(t => !t.IsTransfer && t.OccurredOn >= startDate && t.OccurredOn <= today).ToList();
        var projected = commitments.Where(c => (c.Status is CommitmentStatus.Open or CommitmentStatus.PartiallySettled) && c.DueDate > today && c.DueDate <= endDate).ToList();
        var running = openingBalance;
        var points = new List<CashFlowPoint>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var daily = periodTransactions.Where(t => t.OccurredOn == date).ToList();
            var income = daily.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var expense = daily.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
            var dailyProjected = projected.Where(c => c.DueDate == date).ToList();
            var projectedIncome = dailyProjected.Where(c => c.Kind == CommitmentKind.Receivable).Sum(c => c.RemainingAmount);
            var projectedExpense = dailyProjected.Where(c => c.Kind == CommitmentKind.Payable).Sum(c => c.RemainingAmount);
            // Transfers affect the balance of the selected active accounts, but never revenue/expense.
            running += transactions.Where(transaction => transaction.OccurredOn == date && date <= today).Sum(transaction => transaction.SignedAmount)
                + projectedIncome - projectedExpense;
            points.Add(new CashFlowPoint(date, income, expense, projectedIncome, projectedExpense, running, date > today));
        }
        var totalIncome = periodTransactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var totalExpense = periodTransactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
        var projectedIncomeTotal = projected.Where(c => c.Kind == CommitmentKind.Receivable).Sum(c => c.RemainingAmount);
        var projectedExpenseTotal = projected.Where(c => c.Kind == CommitmentKind.Payable).Sum(c => c.RemainingAmount);
        return new CashFlowSummary(startDate, endDate, openingBalance, currentBalance, currentBalance + projectedIncomeTotal - projectedExpenseTotal, totalIncome, totalExpense, projectedIncomeTotal, projectedExpenseTotal, totalIncome - totalExpense, points);
    }

    public async Task<bool> DeleteTransactionAsync(Guid transactionId, CancellationToken ct)
    {
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        var transaction = transactions.FirstOrDefault(item => item.Id == transactionId);
        if (transaction is null) return false;
        var result = await repository.VoidTransactionAsync(CurrentWorkspaceId, transactionId, userContext.UserId, DateTimeOffset.UtcNow, ct);
        if (result) await AuditAsync("Voided", transaction.IsTransfer ? "Transfer" : "Transaction", transaction.TransferId ?? transaction.Id, $"{(transaction.IsTransfer ? "Transferência" : "Lançamento")} estornado: {transaction.Description}", new { transaction.Amount, transaction.OccurredOn }, ct);
        return result;
    }

    public async Task<BankReconciliationSummary> GetReconciliationAsync(Guid? accountId, CancellationToken ct)
    {
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        var entries = await repository.GetBankStatementEntriesAsync(CurrentWorkspaceId, ct);
        var rules = await repository.GetCategorizationRulesAsync(CurrentWorkspaceId, ct);
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        var history = await repository.GetReconciliationHistoryAsync(CurrentWorkspaceId, ct);
        if (accountId.HasValue) entries = entries.Where(item => item.AccountId == accountId.Value).ToList();
        var reconciledIds = entries.Where(item => item.TransactionId.HasValue).Select(item => item.TransactionId!.Value).ToHashSet();
        var views = new List<BankStatementEntryView>();
        foreach (var entry in entries)
        {
            var suggestion = FindSuggestion(entry, transactions, reconciledIds);
            if (suggestion is not null) reconciledIds.Add(suggestion.TransactionId);
            var rule = entry.Status == ReconciliationStatus.Pending ? rules.FirstOrDefault(candidate => candidate.Matches(entry.AccountId, entry.Description, entry.Type)) : null;
            var categorySuggestion = rule is null ? null : new ReconciliationCategorySuggestion(rule.Name, rule.Category, rule.CostCenterId, centers.FirstOrDefault(center => center.Id == rule.CostCenterId)?.Name);
            var transactionWasCreated = entry.TransactionId.HasValue && history.FirstOrDefault(item => item.BankStatementEntryId == entry.Id && item.TransactionId == entry.TransactionId)?.TransactionWasCreated == true;
            views.Add(ToView(entry, accounts, suggestion, categorySuggestion, transactionWasCreated));
        }
        var selectedAccount = accountId.HasValue ? accounts.FirstOrDefault(item => item.Id == accountId.Value) : null;
        var importedBalance = selectedAccount is null ? accounts.Where(item => item.IsActive).Sum(item => item.OpeningBalance) + entries.Sum(item => item.SignedAmount) : selectedAccount.OpeningBalance + entries.Sum(item => item.SignedAmount);
        var systemBalance = selectedAccount is null
            ? accounts.Where(item => item.IsActive).Sum(item => item.OpeningBalance) + transactions.Sum(item => item.SignedAmount)
            : selectedAccount.OpeningBalance + transactions.Where(item => item.AccountId == selectedAccount.Id).Sum(item => item.SignedAmount);
        return new BankReconciliationSummary(views.Count, views.Count(item => item.Status == nameof(ReconciliationStatus.Pending)), views.Count(item => item.Status == nameof(ReconciliationStatus.Reconciled)), views.Count(item => item.Status == nameof(ReconciliationStatus.Ignored)), importedBalance, systemBalance, importedBalance - systemBalance, views);
    }

    public async Task<IReadOnlyList<ReconciliationHistoryView>> GetReconciliationHistoryAsync(Guid? accountId, CancellationToken ct)
    {
        var history = await repository.GetReconciliationHistoryAsync(CurrentWorkspaceId, ct);
        if (accountId.HasValue) history = history.Where(item => item.AccountId == accountId.Value).ToList();
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        var users = await repository.GetWorkspaceUsersAsync(CurrentWorkspaceId, ct);
        return history.Select(item => new ReconciliationHistoryView(item.Id, item.BankStatementEntryId, item.AccountId, accounts.FirstOrDefault(account => account.Id == item.AccountId)?.Name ?? "Conta", item.UserId, users.FirstOrDefault(user => user.Id == item.UserId)?.Name ?? "Usuário", item.Action, item.TransactionId, item.TransactionWasCreated, item.TransactionRemoved, item.Description, item.Category, item.CostCenterName, item.RuleName, item.Details, item.OccurredAt)).ToList();
    }

    public async Task<BankImportResult> ImportBankStatementAsync(Guid accountId, string fileName, string content, CancellationToken ct)
    {
        var (extension, parsed) = await ParseBankStatementAsync(accountId, fileName, content, ct);
        var existing = await repository.GetBankStatementEntriesAsync(CurrentWorkspaceId, ct);
        var existingKeys = KnownImportKeys(existing, accountId);
        var duplicateCount = 0;
        var valid = new List<BankStatementEntry>();
        foreach (var item in parsed)
        {
            var key = HashImport(accountId, item.ExternalId, item.Date, item.Amount, item.Description);
            if (!existingKeys.Add(key)) { duplicateCount++; continue; }
            valid.Add(new BankStatementEntry { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, AccountId = accountId, ImportKey = key, ExternalId = item.ExternalId, Description = item.Description[..Math.Min(item.Description.Length, 180)], Type = item.Amount >= 0 ? TransactionType.Income : TransactionType.Expense, Amount = Math.Abs(FinancialMoney.Round(item.Amount)), OccurredOn = item.Date, ImportedAt = DateTimeOffset.UtcNow });
        }
        if (valid.Count > 0)
        {
            var history = valid.Select(entry => NewReconciliationHistory(entry, "ImportedPending", null, false, false, null, null, null, $"Importado de {fileName}")).ToList();
            await repository.ImportBankStatementEntriesAsync(valid, [], [], [], history, ct);
        }
        await AuditAsync("Imported", "BankStatement", accountId, $"Extrato {extension.TrimStart('.').ToUpperInvariant()} importado", new { fileName, imported = valid.Count, duplicates = duplicateCount, invalid = parsed.Invalid }, ct);
        return new BankImportResult(valid.Count, duplicateCount, parsed.Invalid);
    }

    public async Task<BankImportPreview> PreviewBankStatementAsync(Guid accountId, string fileName, string content, CancellationToken ct)
    {
        var (extension, parsed) = await ParseBankStatementAsync(accountId, fileName, content, ct);
        var existing = await repository.GetBankStatementEntriesAsync(CurrentWorkspaceId, ct);
        var knownKeys = KnownImportKeys(existing, accountId);
        var reconciledIds = existing.Where(item => item.TransactionId.HasValue).Select(item => item.TransactionId!.Value).ToHashSet();
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        var rules = await repository.GetCategorizationRulesAsync(CurrentWorkspaceId, ct);
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        var rows = new List<BankImportPreviewRow>();
        var ready = 0; var duplicates = 0; decimal totalIncome = 0; decimal totalExpense = 0;
        foreach (var item in parsed)
        {
            var key = HashImport(accountId, item.ExternalId, item.Date, item.Amount, item.Description);
            var duplicate = !knownKeys.Add(key);
            if (duplicate) duplicates++;
            else
            {
                ready++;
                if (item.Amount > 0) totalIncome += FinancialMoney.Round(item.Amount); else totalExpense += Math.Abs(FinancialMoney.Round(item.Amount));
            }
            var match = duplicate ? null : FindImportSuggestion(accountId, item, transactions, reconciledIds);
            if (match is not null) reconciledIds.Add(match.TransactionId);
            var rule = duplicate ? null : rules.FirstOrDefault(candidate => candidate.Matches(accountId, item.Description, item.Amount >= 0 ? TransactionType.Income : TransactionType.Expense));
            var category = rule is null ? null : new BankImportCategorySuggestion(rule.Id, rule.Name, rule.Category, rule.CostCenterId, centers.FirstOrDefault(center => center.Id == rule.CostCenterId)?.Name);
            rows.Add(new BankImportPreviewRow(item.RowNumber, item.ExternalId, item.Description, item.Date, FinancialMoney.Round(item.Amount), duplicate ? "Duplicate" : "Ready", duplicate ? "Este lançamento já foi importado nesta conta." : null, match, category));
        }
        rows.AddRange(parsed.Errors.Select(error => new BankImportPreviewRow(error.RowNumber, null, error.Description, null, null, "Invalid", error.Reason, null, null)));
        return new BankImportPreview(fileName, extension.TrimStart('.').ToUpperInvariant(), parsed.Count + parsed.Invalid, ready, duplicates, parsed.Invalid, totalIncome, totalExpense, rows.OrderBy(row => row.RowNumber).ToList());
    }

    public async Task<BankImportConfirmation> ConfirmBankStatementAsync(Guid accountId, string fileName, string content, IReadOnlyList<BankImportDecision> decisions, CancellationToken ct)
    {
        var (extension, parsed) = await ParseBankStatementAsync(accountId, fileName, content, ct);
        if (decisions.GroupBy(item => item.RowNumber).Any(group => group.Count() > 1)) throw new ArgumentException("Existem decisões repetidas para a mesma linha.");
        var decisionsByRow = decisions.ToDictionary(item => item.RowNumber);
        var existing = await repository.GetBankStatementEntriesAsync(CurrentWorkspaceId, ct);
        var knownKeys = KnownImportKeys(existing, accountId);
        var reconciledIds = existing.Where(item => item.TransactionId.HasValue).Select(item => item.TransactionId!.Value).ToHashSet();
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        var rules = await repository.GetCategorizationRulesAsync(CurrentWorkspaceId, ct);
        var categories = await repository.GetCategoriesAsync(CurrentWorkspaceId, ct);
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        var existingAllocations = await repository.GetAllocationsAsync(CurrentWorkspaceId, ct);
        var entriesToAdd = new List<BankStatementEntry>(); var transactionsToAdd = new List<FinancialTransaction>(); var allocationsToAdd = new List<TransactionAllocation>(); var rulesToAdd = new List<CategorizationRule>(); var historyToAdd = new List<ReconciliationHistoryEntry>();
        var duplicates = 0; var matched = 0; var created = 0; var ignored = 0; var pending = 0;

        foreach (var item in parsed)
        {
            var key = HashImport(accountId, item.ExternalId, item.Date, item.Amount, item.Description);
            if (!knownKeys.Add(key)) { duplicates++; continue; }
            var type = item.Amount >= 0 ? TransactionType.Income : TransactionType.Expense;
            var entry = new BankStatementEntry { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, AccountId = accountId, ImportKey = key, ExternalId = item.ExternalId, Description = item.Description, Type = type, Amount = Math.Abs(FinancialMoney.Round(item.Amount)), OccurredOn = item.Date, ImportedAt = DateTimeOffset.UtcNow };
            decisionsByRow.TryGetValue(item.RowNumber, out var decision);
            var action = decision?.Action.Trim().ToLowerInvariant() ?? "pending";
            var historyAction = "ImportedPending"; Guid? historyTransactionId = null; var transactionWasCreated = false; string? historyCategory = null; string? historyCenter = null; string? historyRule = null;
            if (action == "match")
            {
                if (decision?.TransactionId is not Guid transactionId) throw new ArgumentException($"Selecione o lançamento para conciliar a linha {item.RowNumber}.");
                var transaction = transactions.FirstOrDefault(candidate => candidate.Id == transactionId && candidate.AccountId == accountId && candidate.SignedAmount == FinancialMoney.Round(item.Amount)) ?? throw new ArgumentException($"O lançamento selecionado para a linha {item.RowNumber} não é compatível.");
                if (!reconciledIds.Add(transaction.Id)) throw new ArgumentException($"O lançamento selecionado na linha {item.RowNumber} já está conciliado.");
                entry.Reconcile(transaction.Id); matched++;
                historyAction = "Matched"; historyTransactionId = transaction.Id; historyCategory = transaction.Category;
                var allocation = existingAllocations.FirstOrDefault(existingAllocation => existingAllocation.TransactionId == transaction.Id);
                historyCenter = centers.FirstOrDefault(center => center.Id == allocation?.CostCenterId)?.Name;
            }
            else if (action == "create")
            {
                var rule = rules.FirstOrDefault(candidate => candidate.Matches(accountId, item.Description, type));
                var categoryName = string.IsNullOrWhiteSpace(decision?.Category) ? rule?.Category : decision.Category.Trim();
                var compatibleKinds = type == TransactionType.Income ? new[] { CategoryKind.Income, CategoryKind.Both } : new[] { CategoryKind.Expense, CategoryKind.Both };
                var category = categories.FirstOrDefault(candidate => candidate.IsActive && candidate.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase) && compatibleKinds.Contains(candidate.Kind)) ?? throw new ArgumentException($"Selecione uma categoria compatível para a linha {item.RowNumber}.");
                var centerId = decision?.CostCenterId ?? rule?.CostCenterId;
                if (centerId.HasValue && !centers.Any(center => center.Id == centerId && center.IsActive)) throw new ArgumentException($"O centro de custo da linha {item.RowNumber} não está disponível.");
                var transaction = NewTransaction(accountId, item.Description[..Math.Min(item.Description.Length, 120)], category.Name, type, entry.Amount, item.Date);
                entry.Reconcile(transaction.Id); transactionsToAdd.Add(transaction); created++;
                historyAction = "Created"; historyTransactionId = transaction.Id; transactionWasCreated = true; historyCategory = category.Name; historyCenter = centers.FirstOrDefault(center => center.Id == centerId)?.Name; historyRule = rule?.Name;
                if (centerId is Guid validCenterId) allocationsToAdd.Add(new TransactionAllocation { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, TransactionId = transaction.Id, CostCenterId = validCenterId, Amount = transaction.Amount, CreatedAt = DateTimeOffset.UtcNow });
                if (decision?.SaveAsRule == true)
                {
                    var pattern = (decision.RulePattern ?? item.Description).Trim();
                    if (pattern.Length is < 2 or > 120) throw new ArgumentException($"O padrão da nova regra na linha {item.RowNumber} deve ter entre 2 e 120 caracteres.");
                    var alreadyExists = rules.Concat(rulesToAdd).Any(candidate => candidate.AccountId == accountId && candidate.TransactionType == type && candidate.DescriptionPattern.Equals(pattern, StringComparison.OrdinalIgnoreCase));
                    if (!alreadyExists)
                    {
                        var newRule = new CategorizationRule { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, Name = $"Importação · {pattern[..Math.Min(pattern.Length, 55)]}", DescriptionPattern = pattern, MatchMode = DescriptionMatchMode.Contains, TransactionType = type, AccountId = accountId, Category = category.Name, CostCenterId = centerId, Priority = 100, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
                        rulesToAdd.Add(newRule); historyRule = newRule.Name;
                    }
                }
            }
            else if (action == "ignore") { entry.Ignore(); ignored++; historyAction = "Ignored"; }
            else if (action == "pending") pending++;
            else throw new ArgumentException($"A decisão da linha {item.RowNumber} é inválida.");
            entriesToAdd.Add(entry);
            historyToAdd.Add(NewReconciliationHistory(entry, historyAction, historyTransactionId, transactionWasCreated, false, historyCategory, historyCenter, historyRule, $"Linha {item.RowNumber} de {fileName}"));
        }

        if (entriesToAdd.Count > 0) await repository.ImportBankStatementEntriesAsync(entriesToAdd, transactionsToAdd, allocationsToAdd, rulesToAdd, historyToAdd, ct);
        await AuditAsync("Imported", "BankStatement", accountId, $"Extrato {extension.TrimStart('.').ToUpperInvariant()} revisado e importado", new { fileName, imported = entriesToAdd.Count, matched, created, ignored, pending, duplicates, invalid = parsed.Invalid, rulesCreated = rulesToAdd.Count }, ct);
        return new BankImportConfirmation(entriesToAdd.Count, matched, created, ignored, pending, duplicates, parsed.Invalid, rulesToAdd.Count);
    }

    private async Task<(string Extension, ParsedBankRows Rows)> ParseBankStatementAsync(Guid accountId, string fileName, string content, CancellationToken ct)
    {
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        if (!accounts.Any(item => item.Id == accountId && item.IsActive)) throw new KeyNotFoundException("Conta não encontrada.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".ofx" or ".csv")) throw new ArgumentException("Envie um arquivo OFX ou CSV.");
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("O arquivo está vazio.");
        var parsed = extension == ".ofx" ? ParseOfx(content) : ParseCsv(content);
        if (parsed.Count + parsed.Invalid > 5_000) throw new ArgumentException("O extrato excede o limite de 5.000 lançamentos por arquivo.");
        if (parsed.Count == 0 && parsed.Invalid == 0) throw new ArgumentException("Nenhum lançamento foi encontrado no arquivo.");
        return (extension, parsed);
    }

    public async Task<BankStatementEntryView> MatchBankEntryAsync(Guid entryId, Guid transactionId, CancellationToken ct)
    {
        var entries = await repository.GetBankStatementEntriesAsync(CurrentWorkspaceId, ct);
        var entry = entries.FirstOrDefault(item => item.Id == entryId) ?? throw new KeyNotFoundException("Movimentação bancária não encontrada.");
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        var transaction = transactions.FirstOrDefault(item => item.Id == transactionId && item.AccountId == entry.AccountId) ?? throw new KeyNotFoundException("Lançamento não encontrado nesta conta.");
        if (transaction.SignedAmount != entry.SignedAmount) throw new ArgumentException("O valor do lançamento precisa ser igual ao valor do extrato.");
        if (entries.Any(item => item.Id != entry.Id && item.TransactionId == transactionId)) throw new ArgumentException("Este lançamento já está conciliado.");
        entry.Reconcile(transaction.Id);
        var allocations = await repository.GetAllocationsAsync(CurrentWorkspaceId, ct);
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        var allocation = allocations.FirstOrDefault(item => item.TransactionId == transaction.Id);
        var history = NewReconciliationHistory(entry, "Matched", transaction.Id, false, false, transaction.Category, centers.FirstOrDefault(center => center.Id == allocation?.CostCenterId)?.Name, null, "Conciliação manual");
        await repository.UpdateBankStatementEntryAsync(entry, history, ct);
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        return ToView(entry, accounts, null, null, false);
    }

    public async Task<BankStatementEntryView> CreateFromBankEntryAsync(Guid entryId, CreateFromBankEntryCommand command, CancellationToken ct)
    {
        var entries = await repository.GetBankStatementEntriesAsync(CurrentWorkspaceId, ct);
        var entry = entries.FirstOrDefault(item => item.Id == entryId) ?? throw new KeyNotFoundException("Movimentação bancária não encontrada.");
        var rule = await MatchCategorizationRuleAsync(entry.AccountId, entry.Description, entry.Type, ct);
        var category = rule?.Category ?? command.Category.Trim();
        if (category.Length is < 2 or > 60) throw new ArgumentException("Informe uma categoria válida.");
        var transaction = NewTransaction(entry.AccountId, entry.Description[..Math.Min(entry.Description.Length, 120)], category, entry.Type, entry.Amount, entry.OccurredOn);
        entry.Reconcile(transaction.Id);
        var allocations = rule?.CostCenterId is Guid costCenterId ? new[] { new TransactionAllocation { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, TransactionId = transaction.Id, CostCenterId = costCenterId, Amount = transaction.Amount, CreatedAt = DateTimeOffset.UtcNow } } : [];
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        var history = NewReconciliationHistory(entry, "Created", transaction.Id, true, false, category, centers.FirstOrDefault(center => center.Id == rule?.CostCenterId)?.Name, rule?.Name, "Movimentação criada durante a conciliação");
        await repository.ReconcileWithNewTransactionAsync(entry, transaction, allocations, history, ct);
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        return ToView(entry, accounts, null, null, true);
    }

    public async Task<BankStatementEntryView> SetBankEntryStatusAsync(Guid entryId, bool ignored, CancellationToken ct)
    {
        var entries = await repository.GetBankStatementEntriesAsync(CurrentWorkspaceId, ct);
        var entry = entries.FirstOrDefault(item => item.Id == entryId) ?? throw new KeyNotFoundException("Movimentação bancária não encontrada.");
        if (!ignored && entry.Status == ReconciliationStatus.Reconciled) throw new InvalidOperationException("Use a opção de desfazer conciliação para este registro.");
        if (ignored) entry.Ignore(); else entry.Reopen();
        await repository.UpdateBankStatementEntryAsync(entry, NewReconciliationHistory(entry, ignored ? "Ignored" : "Reopened", null, false, false, null, null, null, ignored ? "Movimentação ignorada" : "Movimentação reaberta"), ct);
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        return ToView(entry, accounts, null, null, false);
    }

    public async Task<BankStatementEntryView> UndoReconciliationAsync(Guid entryId, bool removeCreatedTransaction, CancellationToken ct)
    {
        var entries = await repository.GetBankStatementEntriesAsync(CurrentWorkspaceId, ct);
        var entry = entries.FirstOrDefault(item => item.Id == entryId) ?? throw new KeyNotFoundException("Movimentação bancária não encontrada.");
        if (entry.Status != ReconciliationStatus.Reconciled || entry.TransactionId is not Guid transactionId) throw new InvalidOperationException("Esta movimentação não está conciliada.");
        var reconciliationHistory = await repository.GetReconciliationHistoryAsync(CurrentWorkspaceId, ct);
        var sourceEvent = reconciliationHistory.FirstOrDefault(item => item.BankStatementEntryId == entry.Id && item.TransactionId == transactionId && item.Action is "Created" or "Matched");
        var wasCreated = sourceEvent?.TransactionWasCreated == true;
        if (removeCreatedTransaction && !wasCreated) throw new InvalidOperationException("A movimentação vinculada já existia e não pode ser removida por esta operação.");
        FinancialTransaction? transactionToVoid = null;
        if (removeCreatedTransaction)
        {
            transactionToVoid = (await repository.GetTransactionsAsync(CurrentWorkspaceId, ct)).FirstOrDefault(item => item.Id == transactionId) ?? throw new KeyNotFoundException("Movimentação vinculada não encontrada.");
            transactionToVoid.Void(DateTimeOffset.UtcNow);
        }
        entry.Reopen();
        var action = removeCreatedTransaction ? "UndoneAndRemoved" : "UndoneKeptTransaction";
        var history = NewReconciliationHistory(entry, action, transactionId, wasCreated, removeCreatedTransaction, sourceEvent?.Category, sourceEvent?.CostCenterName, sourceEvent?.RuleName, removeCreatedTransaction ? "Conciliação desfeita e movimentação automática estornada" : "Conciliação desfeita e movimentação mantida");
        await repository.UndoReconciliationAsync(entry, transactionToVoid, history, ct);
        await AuditAsync("ReconciliationUndone", "BankStatementEntry", entry.Id, "Conciliação desfeita", new { TransactionId = transactionId, RemoveCreatedTransaction = removeCreatedTransaction }, ct);
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        return ToView(entry, accounts, null, null, false);
    }

    public async Task<IReadOnlyList<CategoryView>> GetCategoriesAsync(CancellationToken ct)
    {
        var categories = await repository.GetCategoriesAsync(CurrentWorkspaceId, ct);
        return categories.Select(item => new CategoryView(item.Id, item.Name, item.Kind.ToString(), item.ParentId, categories.FirstOrDefault(parent => parent.Id == item.ParentId)?.Name, item.IsActive)).ToList();
    }

    public async Task<CategoryView> CreateCategoryAsync(CreateCategoryCommand command, CancellationToken ct)
    {
        var categories = await repository.GetCategoriesAsync(CurrentWorkspaceId, ct);
        var name = command.Name.Trim();
        if (name.Length is < 2 or > 80) throw new ArgumentException("O nome deve ter entre 2 e 80 caracteres.");
        if (categories.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Já existe uma categoria com este nome.");
        FinancialCategory? parent = null;
        if (command.ParentId.HasValue)
        {
            parent = categories.FirstOrDefault(item => item.Id == command.ParentId.Value) ?? throw new KeyNotFoundException("Categoria principal não encontrada.");
            if (parent.ParentId.HasValue) throw new ArgumentException("A hierarquia permite até dois níveis.");
            if (parent.Kind != CategoryKind.Both && command.Kind != parent.Kind) throw new ArgumentException("A subcategoria deve ter o mesmo tipo da categoria principal.");
        }
        var category = new FinancialCategory { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, Name = name, Kind = command.Kind, ParentId = command.ParentId, CreatedAt = DateTimeOffset.UtcNow };
        await repository.AddCategoryAsync(category, ct);
        return new CategoryView(category.Id, category.Name, category.Kind.ToString(), category.ParentId, parent?.Name, category.IsActive);
    }

    public async Task<CategoryView> SetCategoryActiveAsync(Guid categoryId, bool active, CancellationToken ct)
    {
        var categories = await repository.GetCategoriesAsync(CurrentWorkspaceId, ct);
        var category = categories.FirstOrDefault(item => item.Id == categoryId) ?? throw new KeyNotFoundException("Categoria não encontrada.");
        category.SetActive(active); await repository.UpdateCategoryAsync(category, ct);
        return new CategoryView(category.Id, category.Name, category.Kind.ToString(), category.ParentId, categories.FirstOrDefault(parent => parent.Id == category.ParentId)?.Name, category.IsActive);
    }

    public async Task<IReadOnlyList<CategorizationRuleView>> GetCategorizationRulesAsync(CancellationToken ct)
    {
        var rules = await repository.GetCategorizationRulesAsync(CurrentWorkspaceId, ct);
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        return rules.Select(rule => ToView(rule, accounts, centers)).ToList();
    }

    public async Task<CategorizationRuleView> CreateCategorizationRuleAsync(SaveCategorizationRuleCommand command, CancellationToken ct)
    {
        await ValidateCategorizationRuleAsync(command, ct);
        var transactionType = await ResolveRuleTransactionTypeAsync(command.Category, command.TransactionType, ct);
        var rule = new CategorizationRule { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, Name = command.Name.Trim(), DescriptionPattern = command.DescriptionPattern.Trim(), MatchMode = command.MatchMode, TransactionType = transactionType, AccountId = command.AccountId, Category = command.Category.Trim(), CostCenterId = command.CostCenterId, Priority = command.Priority, IsActive = command.IsActive, CreatedAt = DateTimeOffset.UtcNow };
        await repository.SaveCategorizationRuleAsync(rule, true, ct);
        await AuditAsync("Created", "CategorizationRule", rule.Id, $"Regra criada: {rule.Name}", new { rule.DescriptionPattern, rule.Category, rule.Priority }, ct);
        return ToView(rule, await repository.GetAccountsAsync(CurrentWorkspaceId, ct), await repository.GetCostCentersAsync(CurrentWorkspaceId, ct));
    }

    public async Task<CategorizationRuleView> UpdateCategorizationRuleAsync(Guid ruleId, SaveCategorizationRuleCommand command, CancellationToken ct)
    {
        await ValidateCategorizationRuleAsync(command, ct);
        var rule = (await repository.GetCategorizationRulesAsync(CurrentWorkspaceId, ct)).FirstOrDefault(item => item.Id == ruleId) ?? throw new KeyNotFoundException("Regra não encontrada.");
        rule.Name = command.Name.Trim(); rule.DescriptionPattern = command.DescriptionPattern.Trim(); rule.MatchMode = command.MatchMode; rule.TransactionType = await ResolveRuleTransactionTypeAsync(command.Category, command.TransactionType, ct); rule.AccountId = command.AccountId; rule.Category = command.Category.Trim(); rule.CostCenterId = command.CostCenterId; rule.Priority = command.Priority; rule.IsActive = command.IsActive;
        await repository.SaveCategorizationRuleAsync(rule, false, ct);
        await AuditAsync("Updated", "CategorizationRule", rule.Id, $"Regra atualizada: {rule.Name}", new { rule.DescriptionPattern, rule.Category, rule.Priority }, ct);
        return ToView(rule, await repository.GetAccountsAsync(CurrentWorkspaceId, ct), await repository.GetCostCentersAsync(CurrentWorkspaceId, ct));
    }

    public async Task<bool> DeleteCategorizationRuleAsync(Guid ruleId, CancellationToken ct)
    {
        var rule = (await repository.GetCategorizationRulesAsync(CurrentWorkspaceId, ct)).FirstOrDefault(item => item.Id == ruleId);
        if (rule is null) return false;
        var removed = await repository.DeleteCategorizationRuleAsync(CurrentWorkspaceId, ruleId, ct);
        if (removed) await AuditAsync("Deleted", "CategorizationRule", rule.Id, $"Regra removida: {rule.Name}", null, ct);
        return removed;
    }

    public async Task<CategorizationRulePreview> PreviewCategorizationRuleAsync(Guid ruleId, CancellationToken ct)
    {
        var rule = (await repository.GetCategorizationRulesAsync(CurrentWorkspaceId, ct)).FirstOrDefault(item => item.Id == ruleId) ?? throw new KeyNotFoundException("Regra não encontrada.");
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        var matches = (await repository.GetTransactionsAsync(CurrentWorkspaceId, ct)).Where(item => !item.IsTransfer && rule.Matches(item.AccountId, item.Description, item.Type)).ToList();
        return new CategorizationRulePreview(matches.Count, matches.Take(5).Select(item => ToView(item, accounts)).ToList());
    }

    public async Task<int> ApplyCategorizationRuleAsync(Guid ruleId, CancellationToken ct)
    {
        var rule = (await repository.GetCategorizationRulesAsync(CurrentWorkspaceId, ct)).FirstOrDefault(item => item.Id == ruleId) ?? throw new KeyNotFoundException("Regra não encontrada.");
        var matches = (await repository.GetTransactionsAsync(CurrentWorkspaceId, ct)).Where(item => !item.IsTransfer && rule.Matches(item.AccountId, item.Description, item.Type)).ToList();
        foreach (var transaction in matches) transaction.Update(transaction.AccountId, transaction.Description, rule.Category, transaction.Type, transaction.Amount, transaction.OccurredOn, transaction.CounterpartyAccountId);
        if (matches.Count > 0) await repository.UpdateTransactionsAsync(matches, ct);
        if (rule.CostCenterId is Guid centerId)
            foreach (var transaction in matches) await repository.ReplaceAllocationsAsync(CurrentWorkspaceId, transaction.Id, [new TransactionAllocation { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, TransactionId = transaction.Id, CostCenterId = centerId, Amount = transaction.Amount }], ct);
        await AuditAsync("Applied", "CategorizationRule", rule.Id, $"Regra aplicada a {matches.Count} lançamento(s): {rule.Name}", new { MatchCount = matches.Count }, ct);
        return matches.Count;
    }

    public async Task<IReadOnlyList<AttachmentView>> GetAttachmentsAsync(string entityType, Guid entityId, CancellationToken ct)
    {
        var normalized = NormalizeAttachmentEntityType(entityType);
        await EnsureAttachmentTargetAsync(normalized, entityId, ct);
        return (await repository.GetAttachmentsAsync(CurrentWorkspaceId, ct)).Where(item => item.EntityType == normalized && item.EntityId == entityId).Select(ToView).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetAttachmentCountsAsync(string entityType, CancellationToken ct)
    {
        var normalized = NormalizeAttachmentEntityType(entityType);
        return (await repository.GetAttachmentsAsync(CurrentWorkspaceId, ct)).Where(item => item.EntityType == normalized).GroupBy(item => item.EntityId).ToDictionary(group => group.Key, group => group.Count());
    }

    public async Task<AttachmentView> CreateAttachmentAsync(string entityType, Guid entityId, string originalFileName, string storedFileName, string contentType, long size, CancellationToken ct)
    {
        var normalized = NormalizeAttachmentEntityType(entityType);
        await EnsureAttachmentTargetAsync(normalized, entityId, ct);
        var attachment = new DocumentAttachment { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, EntityType = normalized, EntityId = entityId, OriginalFileName = originalFileName, StoredFileName = storedFileName, ContentType = contentType, Size = size, UploadedByUserId = userContext.UserId, UploadedAt = DateTimeOffset.UtcNow };
        await repository.AddAttachmentAsync(attachment, ct);
        await AuditAsync("Attached", normalized, entityId, $"Anexo incluído: {originalFileName}", new { AttachmentId = attachment.Id, attachment.ContentType, attachment.Size }, ct);
        return ToView(attachment);
    }

    public async Task<DocumentAttachment> GetAttachmentFileAsync(Guid attachmentId, CancellationToken ct)
        => (await repository.GetAttachmentsAsync(CurrentWorkspaceId, ct)).FirstOrDefault(item => item.Id == attachmentId) ?? throw new KeyNotFoundException("Anexo não encontrado.");

    public async Task<DocumentAttachment> DeleteAttachmentAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await repository.DeleteAttachmentAsync(CurrentWorkspaceId, attachmentId, ct) ?? throw new KeyNotFoundException("Anexo não encontrado.");
        await AuditAsync("AttachmentDeleted", attachment.EntityType, attachment.EntityId, $"Anexo removido: {attachment.OriginalFileName}", new { AttachmentId = attachment.Id }, ct);
        return attachment;
    }

    public async Task<IReadOnlyList<CostCenterView>> GetCostCentersAsync(CancellationToken ct)
    {
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        var allocations = await repository.GetAllocationsAsync(CurrentWorkspaceId, ct);
        return centers.Select(item => new CostCenterView(item.Id, item.Name, item.Code, item.IsActive, allocations.Where(allocation => allocation.CostCenterId == item.Id).Sum(allocation => allocation.Amount))).ToList();
    }

    public async Task<CostCenterView> CreateCostCenterAsync(CreateCostCenterCommand command, CancellationToken ct)
    {
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        var name = command.Name.Trim(); var code = command.Code.Trim().ToUpperInvariant();
        if (name.Length is < 2 or > 80) throw new ArgumentException("O nome deve ter entre 2 e 80 caracteres.");
        if (code.Length is < 2 or > 20) throw new ArgumentException("O código deve ter entre 2 e 20 caracteres.");
        if (centers.Any(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Já existe um centro de custo com este código.");
        var center = new CostCenter { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, Name = name, Code = code, CreatedAt = DateTimeOffset.UtcNow };
        await repository.AddCostCenterAsync(center, ct);
        return new CostCenterView(center.Id, center.Name, center.Code, center.IsActive, 0);
    }

    public async Task<CostCenterView> SetCostCenterActiveAsync(Guid costCenterId, bool active, CancellationToken ct)
    {
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        var center = centers.FirstOrDefault(item => item.Id == costCenterId) ?? throw new KeyNotFoundException("Centro de custo não encontrado.");
        center.SetActive(active); await repository.UpdateCostCenterAsync(center, ct);
        var allocations = await repository.GetAllocationsAsync(CurrentWorkspaceId, ct);
        return new CostCenterView(center.Id, center.Name, center.Code, center.IsActive, allocations.Where(item => item.CostCenterId == center.Id).Sum(item => item.Amount));
    }

    public async Task<IReadOnlyList<TransactionAllocationView>> GetAllocationsAsync(Guid? transactionId, CancellationToken ct)
    {
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        var allocations = await repository.GetAllocationsAsync(CurrentWorkspaceId, ct);
        if (transactionId.HasValue) allocations = allocations.Where(item => item.TransactionId == transactionId.Value).ToList();
        return allocations.Select(item => { var total = transactions.FirstOrDefault(transaction => transaction.Id == item.TransactionId)?.Amount ?? item.Amount; var center = centers.First(value => value.Id == item.CostCenterId); return new TransactionAllocationView(item.Id, item.TransactionId, item.CostCenterId, center.Name, center.Code, item.Amount, total == 0 ? 0 : decimal.Round(item.Amount / total * 100, 2)); }).ToList();
    }

    public async Task<IReadOnlyList<TransactionAllocationView>> ReplaceAllocationsAsync(Guid transactionId, IReadOnlyList<AllocationItemCommand> items, CancellationToken ct)
    {
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        var transaction = transactions.FirstOrDefault(item => item.Id == transactionId) ?? throw new KeyNotFoundException("Lançamento não encontrado.");
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        if (items.Count == 0) { await repository.ReplaceAllocationsAsync(CurrentWorkspaceId, transactionId, [], ct); return []; }
        if (items.GroupBy(item => item.CostCenterId).Any(group => group.Count() > 1)) throw new ArgumentException("Não repita o mesmo centro de custo no rateio.");
        if (transaction.IsTransfer) throw new ArgumentException("Transferências não aceitam rateio.");
        if (items.Any(item => FinancialMoney.Round(item.Amount) <= 0 || !centers.Any(center => center.Id == item.CostCenterId && center.IsActive))) throw new ArgumentException("Revise os centros de custo e valores informados.");
        if (items.Sum(item => FinancialMoney.Positive(item.Amount)) != transaction.Amount) throw new ArgumentException($"O rateio deve totalizar {transaction.Amount:C2} após arredondamento.");
        var allocations = items.Select(item => new TransactionAllocation { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, TransactionId = transactionId, CostCenterId = item.CostCenterId, Amount = FinancialMoney.Round(item.Amount), CreatedAt = DateTimeOffset.UtcNow }).ToList();
        await repository.ReplaceAllocationsAsync(CurrentWorkspaceId, transactionId, allocations, ct);
        return allocations.Select(item => { var center = centers.First(value => value.Id == item.CostCenterId); return new TransactionAllocationView(item.Id, item.TransactionId, item.CostCenterId, center.Name, center.Code, item.Amount, decimal.Round(item.Amount / transaction.Amount * 100, 2)); }).ToList();
    }

    public async Task<MonthlyBudgetSummary> GetBudgetSummaryAsync(int year, int month, CancellationToken ct)
    {
        ValidatePeriod(year, month);
        var budgets = (await repository.GetBudgetsAsync(CurrentWorkspaceId, ct)).Where(item => item.Year == year && item.Month == month).ToList();
        var categories = await repository.GetCategoriesAsync(CurrentWorkspaceId, ct);
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        var commitments = await repository.GetCommitmentsAsync(CurrentWorkspaceId, ct);
        var allocations = await repository.GetAllocationsAsync(CurrentWorkspaceId, ct);
        var periodTransactions = transactions.Where(item => !item.IsTransfer && item.OccurredOn.Year == year && item.OccurredOn.Month == month && item.Type == TransactionType.Expense).ToList();
        var periodTransactionIds = periodTransactions.Select(item => item.Id).ToHashSet();
        var openPayables = commitments.Where(item => item.DueDate.Year == year && item.DueDate.Month == month && item.Kind == CommitmentKind.Payable && item.Status is CommitmentStatus.Open or CommitmentStatus.PartiallySettled).ToList();

        var lines = budgets.Select(budget =>
        {
            string name;
            string? code = null;
            decimal actual;
            decimal future = 0;
            if (budget.TargetKind == BudgetTargetKind.Category)
            {
                var category = categories.FirstOrDefault(item => item.Id == budget.TargetId);
                name = category?.Name ?? "Categoria removida";
                actual = periodTransactions.Where(item => item.Category.Equals(name, StringComparison.OrdinalIgnoreCase)).Sum(item => item.Amount);
                future = openPayables.Where(item => item.Category.Equals(name, StringComparison.OrdinalIgnoreCase)).Sum(item => item.RemainingAmount);
            }
            else
            {
                var center = centers.FirstOrDefault(item => item.Id == budget.TargetId);
                name = center?.Name ?? "Centro removido";
                code = center?.Code;
                actual = allocations.Where(item => item.CostCenterId == budget.TargetId && periodTransactionIds.Contains(item.TransactionId)).Sum(item => item.Amount);
            }

            var projected = actual + future;
            var usage = budget.PlannedAmount == 0 ? 0 : decimal.Round(projected / budget.PlannedAmount * 100, 1);
            var status = projected > budget.PlannedAmount ? "Exceeded" : usage >= budget.AlertPercentage ? "Warning" : "Healthy";
            return new BudgetLineView(budget.Id, budget.TargetKind.ToString(), budget.TargetId, name, code, budget.PlannedAmount, actual, projected, budget.PlannedAmount - projected, usage, budget.AlertPercentage, status);
        }).OrderByDescending(item => item.UsagePercentage).ThenBy(item => item.TargetName).ToList();

        return new MonthlyBudgetSummary(year, month, lines.Sum(item => item.PlannedAmount), lines.Sum(item => item.ActualAmount), lines.Sum(item => item.ProjectedAmount), lines.Sum(item => item.AvailableAmount), lines.Count(item => item.Status != "Healthy"), lines);
    }

    public async Task<BudgetLineView> SaveBudgetAsync(SaveBudgetCommand command, CancellationToken ct)
    {
        ValidatePeriod(command.Year, command.Month);
        var categories = await repository.GetCategoriesAsync(CurrentWorkspaceId, ct);
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        if (command.TargetKind == BudgetTargetKind.Category && !categories.Any(item => item.Id == command.TargetId && item.IsActive && item.Kind is CategoryKind.Expense or CategoryKind.Both))
            throw new KeyNotFoundException("Categoria de despesa não encontrada ou inativa.");
        if (command.TargetKind == BudgetTargetKind.CostCenter && !centers.Any(item => item.Id == command.TargetId && item.IsActive))
            throw new KeyNotFoundException("Centro de custo não encontrado ou inativo.");

        var budgets = await repository.GetBudgetsAsync(CurrentWorkspaceId, ct);
        var budget = budgets.FirstOrDefault(item => item.Year == command.Year && item.Month == command.Month && item.TargetKind == command.TargetKind && item.TargetId == command.TargetId);
        var isNew = budget is null;
        budget ??= new MonthlyBudget { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, Year = command.Year, Month = command.Month, TargetKind = command.TargetKind, TargetId = command.TargetId, CreatedAt = DateTimeOffset.UtcNow };
        budget.Update(command.PlannedAmount, command.AlertPercentage);
        await repository.SaveBudgetAsync(budget, isNew, ct);
        return (await GetBudgetSummaryAsync(command.Year, command.Month, ct)).Lines.First(item => item.Id == budget.Id);
    }

    public Task<bool> DeleteBudgetAsync(Guid budgetId, CancellationToken ct) => repository.DeleteBudgetAsync(CurrentWorkspaceId, budgetId, ct);

    public async Task<DreSummary> GetDreAsync(int year, int month, Guid? costCenterId, CancellationToken ct)
    {
        ValidatePeriod(year, month);
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        var categories = await repository.GetCategoriesAsync(CurrentWorkspaceId, ct);
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        var allocations = await repository.GetAllocationsAsync(CurrentWorkspaceId, ct);
        var center = costCenterId.HasValue ? centers.FirstOrDefault(item => item.Id == costCenterId.Value) ?? throw new KeyNotFoundException("Centro de custo não encontrado.") : null;
        var transferCategories = categories.Where(item => item.Kind == CategoryKind.Both).Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var previousDate = new DateOnly(year, month, 1).AddMonths(-1);

        Dictionary<(string Category, TransactionType Type), decimal> Calculate(int targetYear, int targetMonth)
            => transactions
                .Where(item => !item.IsTransfer && item.OccurredOn.Year == targetYear && item.OccurredOn.Month == targetMonth && !transferCategories.Contains(item.Category))
                .Select(item => new
                {
                    item.Category,
                    item.Type,
                    Amount = costCenterId.HasValue
                        ? allocations.Where(allocation => allocation.TransactionId == item.Id && allocation.CostCenterId == costCenterId.Value).Sum(allocation => allocation.Amount)
                        : item.Amount
                })
                .Where(item => item.Amount > 0)
                .GroupBy(item => (item.Category, item.Type))
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

        var current = Calculate(year, month);
        var previous = Calculate(previousDate.Year, previousDate.Month);
        var keys = current.Keys.Concat(previous.Keys).Distinct().ToList();
        var lines = keys.Select(key =>
        {
            var currentAmount = current.GetValueOrDefault(key);
            var previousAmount = previous.GetValueOrDefault(key);
            var variation = previousAmount == 0 ? (currentAmount == 0 ? 0 : 100) : decimal.Round((currentAmount - previousAmount) / previousAmount * 100, 1);
            return new DreCategoryLine(key.Category, key.Type.ToString(), currentAmount, previousAmount, variation);
        }).ToList();
        var revenueLines = lines.Where(item => item.Kind == nameof(TransactionType.Income)).OrderByDescending(item => item.CurrentAmount).ThenBy(item => item.Category).ToList();
        var expenseLines = lines.Where(item => item.Kind == nameof(TransactionType.Expense)).OrderByDescending(item => item.CurrentAmount).ThenBy(item => item.Category).ToList();
        var revenue = revenueLines.Sum(item => item.CurrentAmount);
        var expense = expenseLines.Sum(item => item.CurrentAmount);
        var previousRevenue = revenueLines.Sum(item => item.PreviousAmount);
        var previousExpense = expenseLines.Sum(item => item.PreviousAmount);
        var result = revenue - expense;
        var previousResult = previousRevenue - previousExpense;
        var resultVariation = previousResult == 0 ? (result == 0 ? 0 : 100) : decimal.Round((result - previousResult) / Math.Abs(previousResult) * 100, 1);
        return new DreSummary(year, month, costCenterId, center?.Name, revenue, expense, result, revenue == 0 ? 0 : decimal.Round(result / revenue * 100, 1), previousRevenue, previousExpense, previousResult, resultVariation, revenueLines, expenseLines);
    }

    public async Task<BudgetVarianceReport> GetBudgetVarianceAsync(int year, int month, int months, CancellationToken ct)
    {
        ValidatePeriod(year, month);
        if (months is not (3 or 6 or 12)) throw new ArgumentException("O histórico deve conter 3, 6 ou 12 meses.");
        var current = await GetBudgetSummaryAsync(year, month, ct);
        var lines = current.Lines.Select(item => new BudgetVarianceLine(item.Id, item.TargetKind, item.TargetId, item.TargetName, item.TargetCode, item.PlannedAmount, item.ActualAmount, item.ProjectedAmount - item.ActualAmount, item.ProjectedAmount, item.PlannedAmount - item.ProjectedAmount, item.PlannedAmount == 0 ? 0 : decimal.Round((item.ProjectedAmount - item.PlannedAmount) / item.PlannedAmount * 100, 1), item.AlertPercentage, item.Status)).ToList();
        var periods = new List<BudgetPeriodView>();
        var first = new DateOnly(year, month, 1).AddMonths(-(months - 1));
        for (var index = 0; index < months; index++)
        {
            var date = first.AddMonths(index);
            var summary = date.Year == year && date.Month == month ? current : await GetBudgetSummaryAsync(date.Year, date.Month, ct);
            periods.Add(new BudgetPeriodView(date.Year, date.Month, summary.PlannedAmount, summary.ActualAmount, summary.ProjectedAmount - summary.ActualAmount, summary.ProjectedAmount, summary.AvailableAmount, summary.AlertCount));
        }
        return new BudgetVarianceReport(year, month, current.PlannedAmount, current.ActualAmount, current.ProjectedAmount - current.ActualAmount, current.ProjectedAmount, current.AvailableAmount, current.PlannedAmount == 0 ? 0 : decimal.Round(current.ProjectedAmount / current.PlannedAmount * 100, 1), current.AlertCount, lines, periods);
    }

    public async Task<FinancialGoalsSummary> GetGoalsAsync(CancellationToken ct)
    {
        var goals = await repository.GetGoalsAsync(CurrentWorkspaceId, ct);
        var contributions = await repository.GetGoalContributionsAsync(CurrentWorkspaceId, ct);
        var transactions = await repository.GetTransactionsAsync(CurrentWorkspaceId, ct);
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        var categories = await repository.GetCategoriesAsync(CurrentWorkspaceId, ct);
        var transferCategories = categories.Where(item => item.Kind == CategoryKind.Both).Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var today = Today;
        var views = goals.Select(goal =>
        {
            var goalTransactions = transactions.Where(item => !item.IsTransfer && item.OccurredOn >= goal.StartDate && item.OccurredOn <= today && (goal.Category is not null ? item.Category.Equals(goal.Category, StringComparison.OrdinalIgnoreCase) : !transferCategories.Contains(item.Category))).ToList();
            var goalContributions = contributions.Where(item => item.GoalId == goal.Id).ToList();
            var current = goal.Kind switch
            {
                FinancialGoalKind.Revenue => goalTransactions.Where(item => item.Type == TransactionType.Income).Sum(item => item.Amount),
                FinancialGoalKind.ExpenseLimit => goalTransactions.Where(item => item.Type == TransactionType.Expense).Sum(item => item.Amount),
                FinancialGoalKind.CashBalance => accounts.Where(item => item.IsActive).Sum(item => item.OpeningBalance) + transactions.Where(item => item.OccurredOn <= today).Sum(item => item.SignedAmount),
                _ => goalContributions.Sum(item => item.Amount)
            };
            var elapsedDays = Math.Max(1, today.DayNumber - goal.StartDate.DayNumber + 1);
            var durationDays = Math.Max(1, goal.DueDate.DayNumber - goal.StartDate.DayNumber + 1);
            var dailyPace = current / elapsedDays;
            DateOnly? projectedDate = dailyPace > 0 && goal.Kind != FinancialGoalKind.ExpenseLimit && goal.Kind != FinancialGoalKind.CashBalance ? goal.StartDate.AddDays(Math.Max(0, (int)Math.Ceiling(goal.TargetAmount / dailyPace) - 1)) : null;
            var expenseProjection = goal.Kind == FinancialGoalKind.ExpenseLimit ? current / elapsedDays * durationDays : current;
            var achieved = goal.Kind != FinancialGoalKind.ExpenseLimit && current >= goal.TargetAmount;
            var status = !goal.IsActive ? "Archived"
                : goal.Kind == FinancialGoalKind.ExpenseLimit && current > goal.TargetAmount ? "Exceeded"
                : achieved || (goal.Kind == FinancialGoalKind.ExpenseLimit && today > goal.DueDate) ? "Completed"
                : today > goal.DueDate ? "Overdue"
                : goal.Kind == FinancialGoalKind.ExpenseLimit && expenseProjection > goal.TargetAmount ? "AtRisk"
                : projectedDate.HasValue && projectedDate.Value > goal.DueDate ? "AtRisk"
                : "OnTrack";
            return new FinancialGoalView(goal.Id, goal.Name, goal.Kind.ToString(), goal.TargetAmount, current, goal.TargetAmount - current, decimal.Round(current / goal.TargetAmount * 100, 1), goal.StartDate, goal.DueDate, projectedDate, goal.Category, status, goal.IsActive, goalContributions.Select(item => new GoalContributionView(item.Id, item.Amount, item.ContributedOn, item.Note)).ToList());
        }).OrderBy(item => item.Status == "Completed").ThenBy(item => item.DueDate).ToList();
        var active = views.Where(item => item.IsActive).ToList();
        return new FinancialGoalsSummary(active.Count, active.Count(item => item.Status == "Completed"), active.Count(item => item.Status is "AtRisk" or "Overdue" or "Exceeded"), active.Sum(item => item.TargetAmount), active.Sum(item => item.CurrentAmount), views);
    }

    public async Task<FinancialGoalView> CreateGoalAsync(SaveGoalCommand command, CancellationToken ct)
    {
        await ValidateGoalCategoryAsync(command, ct);
        var goal = new FinancialGoal { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, CreatedAt = DateTimeOffset.UtcNow };
        goal.Update(command.Name, command.Kind, command.TargetAmount, command.StartDate, command.DueDate, command.Category);
        await repository.SaveGoalAsync(goal, true, ct);
        return (await GetGoalsAsync(ct)).Goals.First(item => item.Id == goal.Id);
    }

    public async Task<FinancialGoalView> UpdateGoalAsync(Guid goalId, SaveGoalCommand command, CancellationToken ct)
    {
        await ValidateGoalCategoryAsync(command, ct);
        var goals = await repository.GetGoalsAsync(CurrentWorkspaceId, ct);
        var goal = goals.FirstOrDefault(item => item.Id == goalId) ?? throw new KeyNotFoundException("Meta não encontrada.");
        goal.Update(command.Name, command.Kind, command.TargetAmount, command.StartDate, command.DueDate, command.Category);
        await repository.SaveGoalAsync(goal, false, ct);
        return (await GetGoalsAsync(ct)).Goals.First(item => item.Id == goal.Id);
    }

    public async Task<FinancialGoalView> SetGoalActiveAsync(Guid goalId, bool active, CancellationToken ct)
    {
        var goals = await repository.GetGoalsAsync(CurrentWorkspaceId, ct);
        var goal = goals.FirstOrDefault(item => item.Id == goalId) ?? throw new KeyNotFoundException("Meta não encontrada.");
        goal.SetActive(active); await repository.SaveGoalAsync(goal, false, ct);
        return (await GetGoalsAsync(ct)).Goals.First(item => item.Id == goal.Id);
    }

    public async Task<FinancialGoalView> AddGoalContributionAsync(Guid goalId, AddGoalContributionCommand command, CancellationToken ct)
    {
        var goals = await repository.GetGoalsAsync(CurrentWorkspaceId, ct);
        var goal = goals.FirstOrDefault(item => item.Id == goalId && item.IsActive) ?? throw new KeyNotFoundException("Meta ativa não encontrada.");
        if (goal.Kind is not (FinancialGoalKind.EmergencyReserve or FinancialGoalKind.Custom)) throw new ArgumentException("Aportes manuais são permitidos em metas de reserva ou personalizadas.");
        FinancialMoney.Positive(command.Amount);
        if (command.ContributedOn > Today) throw new ArgumentException("A data do aporte não pode estar no futuro.");
        var note = command.Note.Trim(); if (note.Length > 160) throw new ArgumentException("A observação deve ter até 160 caracteres.");
        await repository.AddGoalContributionAsync(new GoalContribution { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, GoalId = goal.Id, Amount = FinancialMoney.Round(command.Amount), ContributedOn = command.ContributedOn, Note = note, CreatedAt = DateTimeOffset.UtcNow }, ct);
        return (await GetGoalsAsync(ct)).Goals.First(item => item.Id == goal.Id);
    }

    public async Task<IReadOnlyList<ContactView>> GetContactsAsync(bool includeInactive, CancellationToken ct)
    {
        var contacts = await repository.GetContactsAsync(CurrentWorkspaceId, ct);
        var commitments = await repository.GetCommitmentsAsync(CurrentWorkspaceId, ct);
        return contacts.Where(item => includeInactive || item.IsActive).Select(item => ToView(item, commitments)).ToList();
    }

    public async Task<ContactView> CreateContactAsync(SaveContactCommand command, CancellationToken ct)
    {
        ValidateContact(command);
        var contacts = await repository.GetContactsAsync(CurrentWorkspaceId, ct);
        if (contacts.Any(item => item.Name.Equals(command.Name.Trim(), StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Já existe um contato com esse nome.");
        var contact = new Contact { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, Name = command.Name.Trim(), Kind = command.Kind, TaxId = CleanOptional(command.TaxId), Email = CleanOptional(command.Email), Phone = CleanOptional(command.Phone), CreatedAt = DateTimeOffset.UtcNow };
        await repository.SaveContactAsync(contact, true, ct);
        await AuditAsync("Created", "Contact", contact.Id, $"Contato criado: {contact.Name}", new { contact.Kind, contact.TaxId }, ct);
        return ToView(contact, []);
    }

    public async Task<ContactView> UpdateContactAsync(Guid contactId, SaveContactCommand command, CancellationToken ct)
    {
        ValidateContact(command);
        var contacts = await repository.GetContactsAsync(CurrentWorkspaceId, ct);
        var contact = contacts.FirstOrDefault(item => item.Id == contactId) ?? throw new KeyNotFoundException("Contato não encontrado.");
        if (contacts.Any(item => item.Id != contactId && item.Name.Equals(command.Name.Trim(), StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Já existe um contato com esse nome.");
        contact.Update(command.Name.Trim(), command.Kind, CleanOptional(command.TaxId), CleanOptional(command.Email), CleanOptional(command.Phone));
        await repository.SaveContactAsync(contact, false, ct);
        await AuditAsync("Updated", "Contact", contact.Id, $"Contato editado: {contact.Name}", new { contact.Kind, contact.TaxId }, ct);
        return ToView(contact, await repository.GetCommitmentsAsync(CurrentWorkspaceId, ct));
    }

    public async Task<ContactView> SetContactActiveAsync(Guid contactId, bool active, CancellationToken ct)
    {
        var contacts = await repository.GetContactsAsync(CurrentWorkspaceId, ct);
        var contact = contacts.FirstOrDefault(item => item.Id == contactId) ?? throw new KeyNotFoundException("Contato não encontrado.");
        contact.SetActive(active); await repository.SaveContactAsync(contact, false, ct);
        await AuditAsync(active ? "Activated" : "Archived", "Contact", contact.Id, $"Contato {(active ? "ativado" : "arquivado")}: {contact.Name}", null, ct);
        return ToView(contact, await repository.GetCommitmentsAsync(CurrentWorkspaceId, ct));
    }

    public async Task<IReadOnlyList<AuditEntryView>> GetAuditEntriesAsync(CancellationToken ct)
        => (await repository.GetAuditEntriesAsync(CurrentWorkspaceId, ct)).Select(item => new AuditEntryView(item.Id, item.Action, item.EntityType, item.EntityId, item.Description, item.Details, item.UserId, item.OccurredAt)).ToList();

    private async Task<string> ResolveContactAsync(Guid? contactId, string fallback, CommitmentKind kind, CancellationToken ct)
    {
        if (!contactId.HasValue)
        {
            var name = fallback.Trim();
            if (name.Length is < 2 or > 100) throw new ArgumentException("O contato deve ter entre 2 e 100 caracteres.");
            return name;
        }
        var contact = (await repository.GetContactsAsync(CurrentWorkspaceId, ct)).FirstOrDefault(item => item.Id == contactId && item.IsActive) ?? throw new KeyNotFoundException("Contato não encontrado ou inativo.");
        var compatible = kind == CommitmentKind.Receivable ? contact.Kind is ContactKind.Customer or ContactKind.Both : contact.Kind is ContactKind.Supplier or ContactKind.Both;
        if (!compatible) throw new ArgumentException(kind == CommitmentKind.Receivable ? "Selecione um cliente para a conta a receber." : "Selecione um fornecedor para a conta a pagar.");
        return contact.Name;
    }

    private Task AuditAsync(string action, string entityType, Guid entityId, string description, object? details, CancellationToken ct)
        => repository.AddAuditEntryAsync(new AuditEntry { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, UserId = userContext.UserId, Action = action, EntityType = entityType, EntityId = entityId, Description = description, Details = details is null ? null : JsonSerializer.Serialize(details), OccurredAt = DateTimeOffset.UtcNow }, ct);

    private ReconciliationHistoryEntry NewReconciliationHistory(BankStatementEntry entry, string action, Guid? transactionId, bool transactionWasCreated, bool transactionRemoved, string? category, string? costCenterName, string? ruleName, string? details)
        => new() { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, BankStatementEntryId = entry.Id, AccountId = entry.AccountId, UserId = userContext.UserId, Action = action, TransactionId = transactionId, TransactionWasCreated = transactionWasCreated, TransactionRemoved = transactionRemoved, Description = entry.Description, Category = category, CostCenterName = costCenterName, RuleName = ruleName, Details = details, OccurredAt = DateTimeOffset.UtcNow };

    private static void ValidateContact(SaveContactCommand command)
    {
        if (command.Name.Trim().Length is < 2 or > 100) throw new ArgumentException("O nome deve ter entre 2 e 100 caracteres.");
        if (!string.IsNullOrWhiteSpace(command.Email) && (!command.Email.Contains('@') || command.Email.Trim().Length > 254)) throw new ArgumentException("Informe um e-mail válido.");
        if (command.TaxId?.Trim().Length > 20) throw new ArgumentException("O CPF/CNPJ é muito longo.");
        if (command.Phone?.Trim().Length > 30) throw new ArgumentException("O telefone é muito longo.");
    }

    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<CategorizationRule?> MatchCategorizationRuleAsync(Guid accountId, string description, TransactionType type, CancellationToken ct)
        => (await repository.GetCategorizationRulesAsync(CurrentWorkspaceId, ct)).FirstOrDefault(rule => rule.Matches(accountId, description.Trim(), type));

    private async Task ValidateCategorizationRuleAsync(SaveCategorizationRuleCommand command, CancellationToken ct)
    {
        if (command.Name.Trim().Length is < 2 or > 80) throw new ArgumentException("O nome da regra deve ter entre 2 e 80 caracteres.");
        if (command.DescriptionPattern.Trim().Length is < 2 or > 120) throw new ArgumentException("Informe ao menos dois caracteres para comparar na descrição.");
        if (command.Category.Trim().Length is < 2 or > 80) throw new ArgumentException("Selecione uma categoria válida.");
        if (command.Priority is < 1 or > 1000) throw new ArgumentException("A prioridade deve estar entre 1 e 1000.");
        var accounts = await repository.GetAccountsAsync(CurrentWorkspaceId, ct);
        if (command.AccountId.HasValue && !accounts.Any(item => item.Id == command.AccountId && item.IsActive)) throw new KeyNotFoundException("Conta não encontrada.");
        var categories = await repository.GetCategoriesAsync(CurrentWorkspaceId, ct);
        var category = categories.FirstOrDefault(item => item.IsActive && item.Name.Equals(command.Category.Trim(), StringComparison.OrdinalIgnoreCase)) ?? throw new KeyNotFoundException("Categoria não encontrada.");
        if (command.TransactionType == TransactionType.Income && category.Kind == CategoryKind.Expense || command.TransactionType == TransactionType.Expense && category.Kind == CategoryKind.Income) throw new ArgumentException("A categoria não é compatível com o tipo de movimentação.");
        var centers = await repository.GetCostCentersAsync(CurrentWorkspaceId, ct);
        if (command.CostCenterId.HasValue && !centers.Any(item => item.Id == command.CostCenterId && item.IsActive)) throw new KeyNotFoundException("Centro de custo não encontrado.");
    }

    private async Task<TransactionType?> ResolveRuleTransactionTypeAsync(string categoryName, TransactionType? requested, CancellationToken ct)
    {
        if (requested.HasValue) return requested;
        var category = (await repository.GetCategoriesAsync(CurrentWorkspaceId, ct)).First(item => item.IsActive && item.Name.Equals(categoryName.Trim(), StringComparison.OrdinalIgnoreCase));
        return category.Kind == CategoryKind.Income ? TransactionType.Income : category.Kind == CategoryKind.Expense ? TransactionType.Expense : null;
    }

    private static string NormalizeAttachmentEntityType(string entityType)
        => entityType.Trim().ToLowerInvariant() switch { "transaction" => "Transaction", "commitment" => "Commitment", _ => throw new ArgumentException("Anexos são permitidos apenas em movimentações e compromissos.") };

    private async Task EnsureAttachmentTargetAsync(string entityType, Guid entityId, CancellationToken ct)
    {
        var exists = entityType == "Transaction"
            ? (await repository.GetTransactionsAsync(CurrentWorkspaceId, ct)).Any(item => item.Id == entityId)
            : (await repository.GetCommitmentsAsync(CurrentWorkspaceId, ct)).Any(item => item.Id == entityId);
        if (!exists) throw new KeyNotFoundException("Registro financeiro não encontrado.");
    }

    private async Task ValidateGoalCategoryAsync(SaveGoalCommand command, CancellationToken ct)
    {
        if (command.Kind is not (FinancialGoalKind.Revenue or FinancialGoalKind.ExpenseLimit) && !string.IsNullOrWhiteSpace(command.Category)) throw new ArgumentException("Categorias só podem ser vinculadas a metas de receita ou despesa.");
        if (string.IsNullOrWhiteSpace(command.Category)) return;
        var categories = await repository.GetCategoriesAsync(CurrentWorkspaceId, ct);
        var validKinds = command.Kind == FinancialGoalKind.Revenue ? new[] { CategoryKind.Income, CategoryKind.Both } : new[] { CategoryKind.Expense, CategoryKind.Both };
        if (!categories.Any(item => item.IsActive && item.Name.Equals(command.Category.Trim(), StringComparison.OrdinalIgnoreCase) && validKinds.Contains(item.Kind))) throw new KeyNotFoundException("Categoria compatível não encontrada.");
    }

    private static void ValidatePeriod(int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12) throw new ArgumentException("Informe um mês e ano válidos.");
    }

    private static void ValidateTextAndAmount(string description, string category, decimal amount)
    {
        if (description.Trim().Length is < 2 or > 120) throw new ArgumentException("A descrição deve ter entre 2 e 120 caracteres.");
        if (category.Trim().Length is < 2 or > 60) throw new ArgumentException("A categoria deve ter entre 2 e 60 caracteres.");
        FinancialMoney.Positive(amount);
    }

    private static void ValidateAccount(SaveAccountCommand command)
    {
        if (command.Name.Trim().Length is < 2 or > 80) throw new ArgumentException("O nome deve ter entre 2 e 80 caracteres.");
        if (command.Institution.Trim().Length is < 2 or > 80) throw new ArgumentException("A instituição deve ter entre 2 e 80 caracteres.");
        var digits = NormalizeDigits(command.LastFourDigits);
        if (digits is not null && (digits.Length != 4 || !digits.All(char.IsDigit))) throw new ArgumentException("Informe exatamente os quatro últimos dígitos.");
        if (command.OpeningBalance is < -FinancialMoney.Maximum or > FinancialMoney.Maximum) throw new ArgumentException("O saldo inicial está fora do limite permitido.");
        if (!Enum.IsDefined(command.Type)) throw new ArgumentException("Tipo de conta inválido.");
    }

    private static void ValidateWorkspace(SaveWorkspaceCommand command)
    {
        if (command.Name.Trim().Length is < 2 or > 80) throw new ArgumentException("O nome do espaço deve ter entre 2 e 80 caracteres.");
    }

    private static string? NormalizeDigits(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private FinancialTransaction NewTransaction(Guid accountId, string description, string category, TransactionType type, decimal amount, DateOnly occurredOn, Guid? commitmentId = null)
        => new() { Id = Guid.NewGuid(), WorkspaceId = CurrentWorkspaceId, AccountId = accountId, Description = description, Category = category, Type = type, Amount = FinancialMoney.Positive(amount), OccurredOn = occurredOn, CreatedAt = DateTimeOffset.UtcNow, CommitmentId = commitmentId };
    private static void ValidateFinancialDate(DateOnly date)
    {
        if (date.Year is < 2000 or > 2100) throw new ArgumentException("A data deve estar entre os anos 2000 e 2100.");
    }
    private void ValidateMovementDate(DateOnly date)
    {
        ValidateFinancialDate(date);
        if (date > Today) throw new ArgumentException("Movimentações e baixas devem ter data até hoje. Use um compromisso para valores futuros.");
    }
    private static DateOnly AddFrequency(DateOnly date, RecurrenceFrequency? frequency, int index)
        => frequency switch
        {
            RecurrenceFrequency.Weekly => date.AddDays(index * 7),
            RecurrenceFrequency.Yearly => date.AddYears(index),
            _ => date.AddMonths(index)
        };
    private AccountView ToView(FinancialAccount account, IReadOnlyList<FinancialTransaction> transactions)
    {
        var accountTransactions = transactions.Where(transaction => transaction.AccountId == account.Id && transaction.OccurredOn <= Today).ToList();
        return new(account.Id, account.Name, account.Institution, account.LastFourDigits, account.Type.ToString(), account.OpeningBalance, account.OpeningBalance + accountTransactions.Sum(transaction => transaction.SignedAmount), account.IsActive, accountTransactions.Count);
    }
    private static WorkspaceView ToView(Workspace workspace, WorkspaceRole role) => new(workspace.Id, workspace.Name, workspace.Kind.ToString(), role.ToString(), workspace.CreatedAt);
    private static TransactionView ToView(FinancialTransaction transaction, IReadOnlyList<FinancialAccount> accounts)
        => new(transaction.Id, transaction.AccountId, accounts.FirstOrDefault(a => a.Id == transaction.AccountId)?.Name ?? "Conta", transaction.Description, transaction.Category, transaction.Type.ToString(), transaction.Amount, transaction.SignedAmount, transaction.OccurredOn, transaction.CreatedAt, transaction.TransferId, transaction.CounterpartyAccountId, accounts.FirstOrDefault(a => a.Id == transaction.CounterpartyAccountId)?.Name);
    private CommitmentView ToView(FinancialCommitment commitment)
        => new(commitment.Id, commitment.SeriesId, commitment.ContactId, commitment.Description, commitment.Contact, commitment.Category, commitment.Kind.ToString(), commitment.TotalAmount, commitment.SettledAmount, commitment.RemainingAmount, commitment.DueDate, commitment.CompetenceDate, commitment.InstallmentNumber, commitment.InstallmentCount, commitment.Status.ToString(), (commitment.Status is CommitmentStatus.Open or CommitmentStatus.PartiallySettled) && commitment.DueDate < Today, commitment.RecurrenceFrequency?.ToString(), commitment.RecurrenceNumber, commitment.RecurrenceCount);
    private static ContactView ToView(Contact contact, IReadOnlyList<FinancialCommitment> commitments)
        => new(contact.Id, contact.Name, contact.Kind.ToString(), contact.TaxId, contact.Email, contact.Phone, contact.IsActive, commitments.Count(item => item.ContactId == contact.Id));
    private static CategorizationRuleView ToView(CategorizationRule rule, IReadOnlyList<FinancialAccount> accounts, IReadOnlyList<CostCenter> centers)
        => new(rule.Id, rule.Name, rule.DescriptionPattern, rule.MatchMode.ToString(), rule.TransactionType?.ToString(), rule.AccountId, accounts.FirstOrDefault(item => item.Id == rule.AccountId)?.Name, rule.Category, rule.CostCenterId, centers.FirstOrDefault(item => item.Id == rule.CostCenterId)?.Name, rule.Priority, rule.IsActive);
    private static AttachmentView ToView(DocumentAttachment attachment)
        => new(attachment.Id, attachment.EntityType, attachment.EntityId, attachment.OriginalFileName, attachment.ContentType, attachment.Size, attachment.UploadedByUserId, attachment.UploadedAt);

    private static BankStatementEntryView ToView(BankStatementEntry entry, IReadOnlyList<FinancialAccount> accounts, ReconciliationSuggestion? suggestion, ReconciliationCategorySuggestion? categorySuggestion = null, bool transactionWasCreated = false)
        => new(entry.Id, entry.AccountId, accounts.FirstOrDefault(item => item.Id == entry.AccountId)?.Name ?? "Conta", entry.Description, entry.Type.ToString(), entry.Amount, entry.SignedAmount, entry.OccurredOn, entry.Status.ToString(), entry.TransactionId, transactionWasCreated, suggestion, categorySuggestion);

    private static ReconciliationSuggestion? FindSuggestion(BankStatementEntry entry, IReadOnlyList<FinancialTransaction> transactions, HashSet<Guid> reconciledIds)
    {
        if (entry.Status != ReconciliationStatus.Pending) return null;
        var candidate = transactions.Where(item => item.AccountId == entry.AccountId && item.SignedAmount == entry.SignedAmount && !reconciledIds.Contains(item.Id))
            .Select(item => new { Transaction = item, Distance = Math.Abs(item.OccurredOn.DayNumber - entry.OccurredOn.DayNumber), Similarity = DescriptionSimilarity(entry.Description, item.Description) })
            .Where(item => item.Distance <= 7).Select(item => new { item.Transaction, item.Distance, Score = DateScore(item.Distance) + item.Similarity / 2 })
            .OrderByDescending(item => item.Score).ThenBy(item => item.Distance).FirstOrDefault();
        if (candidate is null) return null;
        var confidence = candidate.Score >= 75 ? "High" : candidate.Score >= 50 ? "Medium" : "Low";
        return new ReconciliationSuggestion(candidate.Transaction.Id, candidate.Transaction.Description, candidate.Transaction.Category, candidate.Transaction.OccurredOn, candidate.Transaction.SignedAmount, candidate.Distance, candidate.Score, confidence);
    }

    private static BankImportMatchSuggestion? FindImportSuggestion(Guid accountId, ParsedBankRow row, IReadOnlyList<FinancialTransaction> transactions, HashSet<Guid> reconciledIds)
    {
        var signedAmount = FinancialMoney.Round(row.Amount);
        var candidate = transactions.Where(item => item.AccountId == accountId && item.SignedAmount == signedAmount && !reconciledIds.Contains(item.Id))
            .Select(item => new { Transaction = item, Distance = Math.Abs(item.OccurredOn.DayNumber - row.Date.DayNumber), Similarity = DescriptionSimilarity(row.Description, item.Description) })
            .Where(item => item.Distance <= 7)
            .Select(item => new { item.Transaction, item.Distance, item.Similarity, Score = DateScore(item.Distance) + item.Similarity / 2 })
            .OrderByDescending(item => item.Score).ThenBy(item => item.Distance).FirstOrDefault();
        if (candidate is null) return null;
        var confidence = candidate.Score >= 75 ? "High" : candidate.Score >= 50 ? "Medium" : "Low";
        return new BankImportMatchSuggestion(candidate.Transaction.Id, candidate.Transaction.Description, candidate.Transaction.Category, candidate.Transaction.OccurredOn, candidate.Transaction.SignedAmount, candidate.Distance, candidate.Score, confidence);
    }

    private static int DateScore(int distance) => distance switch { 0 => 50, 1 => 42, <= 3 => 30, _ => 15 };
    private static int DescriptionSimilarity(string left, string right)
    {
        static HashSet<string> Words(string value) => value.Split([' ', '-', '/', '.'], StringSplitOptions.RemoveEmptyEntries).Select(word => word.ToUpperInvariant()).ToHashSet();
        var leftWords = Words(left); var rightWords = Words(right);
        if (leftWords.Count == 0 || rightWords.Count == 0) return 0;
        var common = leftWords.Intersect(rightWords).Count();
        return (int)Math.Round(common * 100m / leftWords.Union(rightWords).Count());
    }

    private static string HashImport(Guid accountId, string? externalId, DateOnly date, decimal amount, string description)
    {
        var source = externalId is { Length: > 0 } ? $"{accountId:N}|{externalId.Trim()}" : $"{accountId:N}|{date:yyyyMMdd}|{FinancialMoney.Round(amount).ToString("0.00", CultureInfo.InvariantCulture)}|{description.Trim().ToUpperInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }
    private static HashSet<string> KnownImportKeys(IReadOnlyList<BankStatementEntry> entries, Guid accountId)
        => entries.Where(item => item.AccountId == accountId)
            .SelectMany(item => new[] { item.ImportKey, HashImport(accountId, item.ExternalId, item.OccurredOn, item.SignedAmount, item.Description) }).ToHashSet();

    private ParsedBankRows ParseOfx(string content)
    {
        var rows = new List<ParsedBankRow>(); var errors = new List<ParsedBankError>(); var rowNumber = 0;
        foreach (Match block in Regex.Matches(content, @"<STMTTRN>(.*?)(?:</STMTTRN>|(?=<STMTTRN>)|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            rowNumber++;
            var text = block.Groups[1].Value;
            var dateText = Tag(text, "DTPOSTED"); var amountText = Tag(text, "TRNAMT");
            var description = LimitDescription(Tag(text, "NAME") ?? Tag(text, "MEMO") ?? "Movimentação bancária");
            if (!TryDate(dateText, out var date) || !ValidImportDate(date)) { errors.Add(new ParsedBankError(rowNumber, description, "Data ausente, inválida ou futura.")); continue; }
            if (!decimal.TryParse(amountText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || !ValidImportAmount(amount)) { errors.Add(new ParsedBankError(rowNumber, description, "Valor ausente, inválido ou inferior a um centavo.")); continue; }
            rows.Add(new ParsedBankRow(rowNumber, date, amount, description, Tag(text, "FITID")));
        }
        return new ParsedBankRows(rows, errors);
    }

    private ParsedBankRows ParseCsv(string content)
    {
        var lines = content.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries); if (lines.Length < 2) throw new ArgumentException("O CSV não contém dados.");
        var delimiter = lines[0].Count(ch => ch == ';') >= lines[0].Count(ch => ch == ',') ? ';' : ',';
        var headers = SplitCsv(lines[0], delimiter).Select(NormalizeHeader).ToList();
        int Find(params string[] names) => headers.FindIndex(header => names.Contains(header));
        var dateIndex = Find("data", "date", "dtposted"); var descriptionIndex = Find("descricao", "historico", "description", "memo"); var amountIndex = Find("valor", "amount", "trnamt"); var idIndex = Find("id", "fitid", "documento");
        if (dateIndex < 0 || descriptionIndex < 0 || amountIndex < 0) throw new ArgumentException("O CSV precisa ter as colunas Data, Descrição e Valor.");
        var rows = new List<ParsedBankRow>(); var errors = new List<ParsedBankError>(); var rowNumber = 1;
        foreach (var line in lines.Skip(1))
        {
            rowNumber++;
            var fields = SplitCsv(line, delimiter);
            if (fields.Count <= Math.Max(amountIndex, Math.Max(dateIndex, descriptionIndex))) { errors.Add(new ParsedBankError(rowNumber, "Linha incompleta", "Faltam colunas obrigatórias.")); continue; }
            var description = LimitDescription(fields[descriptionIndex]);
            if (!TryDate(fields[dateIndex], out var date) || !ValidImportDate(date)) { errors.Add(new ParsedBankError(rowNumber, description, "Data ausente, inválida ou futura.")); continue; }
            if (!TryDecimal(fields[amountIndex], out var amount) || !ValidImportAmount(amount)) { errors.Add(new ParsedBankError(rowNumber, description, "Valor ausente, inválido ou inferior a um centavo.")); continue; }
            if (string.IsNullOrWhiteSpace(description)) { errors.Add(new ParsedBankError(rowNumber, "Sem descrição", "A descrição é obrigatória.")); continue; }
            rows.Add(new ParsedBankRow(rowNumber, date, amount, description, idIndex >= 0 && idIndex < fields.Count ? fields[idIndex].Trim() : null));
        }
        return new ParsedBankRows(rows, errors);
    }

    private static string? Tag(string content, string name) { var match = Regex.Match(content, $@"<{name}>([^<\r\n]+)", RegexOptions.IgnoreCase); return match.Success ? match.Groups[1].Value.Trim() : null; }
    private bool ValidImportDate(DateOnly date) => date.Year is >= 2000 and <= 2100 && date <= Today;
    private static bool ValidImportAmount(decimal amount) => amount is >= -FinancialMoney.Maximum and <= FinancialMoney.Maximum && FinancialMoney.Round(amount) != 0;
    private static bool TryDate(string? value, out DateOnly date)
    {
        var clean = value?.Trim();
        if (clean is not null && Regex.IsMatch(clean, @"^\d{8}(?:\d{6}(?:\.\d+)?(?:\[[^\]]+\])?)?$"))
            return DateOnly.TryParseExact(clean[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        return DateOnly.TryParseExact(clean, ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
    private static bool TryDecimal(string value, out decimal amount)
    {
        var clean = value.Replace("R$", "", StringComparison.OrdinalIgnoreCase).Trim();
        var culture = clean.LastIndexOf(',') > clean.LastIndexOf('.') ? new CultureInfo("pt-BR") : CultureInfo.InvariantCulture;
        return decimal.TryParse(clean, NumberStyles.Number | NumberStyles.AllowLeadingSign, culture, out amount);
    }
    private static string NormalizeHeader(string value) { var normalized = value.Trim().TrimStart('\uFEFF').ToLowerInvariant().Normalize(NormalizationForm.FormD); return new string(normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray()); }
    private static string LimitDescription(string value) { var trimmed = value.Trim(); return trimmed[..Math.Min(trimmed.Length, 180)]; }
    private static List<string> SplitCsv(string line, char delimiter) { var result = new List<string>(); var current = new StringBuilder(); var quoted = false; for (var index = 0; index < line.Length; index++) { var ch = line[index]; if (ch == '"') { if (quoted && index + 1 < line.Length && line[index + 1] == '"') { current.Append('"'); index++; } else quoted = !quoted; } else if (ch == delimiter && !quoted) { result.Add(current.ToString()); current.Clear(); } else current.Append(ch); } result.Add(current.ToString()); return result; }
    private sealed record ParsedBankRow(int RowNumber, DateOnly Date, decimal Amount, string Description, string? ExternalId);
    private sealed record ParsedBankError(int RowNumber, string Description, string Reason);
    private sealed record ParsedBankRows(IReadOnlyList<ParsedBankRow> Rows, IReadOnlyList<ParsedBankError> Errors) : IReadOnlyList<ParsedBankRow> { public int Count => Rows.Count; public int Invalid => Errors.Count; public ParsedBankRow this[int index] => Rows[index]; public IEnumerator<ParsedBankRow> GetEnumerator() => Rows.GetEnumerator(); System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator(); }
}
