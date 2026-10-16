using Financeiro.Application;
using Financeiro.Domain;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infrastructure;

public sealed class SqliteFinanceRepository(FinanceDbContext database) : IFinanceRepository
{
    public async Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(CancellationToken cancellationToken)
        => await database.Workspaces.AsNoTracking().OrderBy(workspace => workspace.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WorkspaceMember>> GetWorkspaceMembershipsAsync(Guid userId, CancellationToken cancellationToken)
        => await database.WorkspaceMembers.AsNoTracking().Where(member => member.UserId == userId).ToListAsync(cancellationToken);

    public async Task SaveWorkspaceAsync(Workspace workspace, bool isNew, CancellationToken cancellationToken)
    {
        if (isNew) database.Workspaces.Add(workspace);
        else database.Workspaces.Update(workspace);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveWorkspaceWithOwnerAsync(Workspace workspace, WorkspaceMember owner, CancellationToken cancellationToken)
    {
        database.Workspaces.Add(workspace);
        database.WorkspaceMembers.Add(owner);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialAccount>> GetAccountsAsync(Guid workspaceId, CancellationToken cancellationToken)
        => await database.Accounts.AsNoTracking().Where(account => account.WorkspaceId == workspaceId).OrderBy(account => account.Name).ToListAsync(cancellationToken);

    public async Task SaveAccountAsync(FinancialAccount account, bool isNew, CancellationToken cancellationToken)
    {
        if (isNew) database.Accounts.Add(account);
        else database.Accounts.Update(account);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialTransaction>> GetTransactionsAsync(Guid workspaceId, CancellationToken cancellationToken)
        => await database.Transactions.AsNoTracking().Where(transaction => transaction.WorkspaceId == workspaceId && !transaction.IsVoided).ToListAsync(cancellationToken);

    public async Task AddTransactionAsync(FinancialTransaction transaction, CancellationToken cancellationToken)
    {
        database.Transactions.Add(transaction);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTransactionsAsync(IReadOnlyList<FinancialTransaction> transactions, CancellationToken cancellationToken)
    {
        database.Transactions.AddRange(transactions);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateTransactionsAsync(IReadOnlyList<FinancialTransaction> transactions, CancellationToken cancellationToken)
    {
        database.Transactions.UpdateRange(transactions);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> VoidTransactionAsync(Guid workspaceId, Guid transactionId, Guid userId, DateTimeOffset voidedAt, CancellationToken cancellationToken)
    {
        await using var unit = await database.Database.BeginTransactionAsync(cancellationToken);
        var transaction = await database.Transactions.FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.Id == transactionId && !item.IsVoided, cancellationToken);
        if (transaction is null) return false;
        var transactions = transaction.TransferId.HasValue
            ? await database.Transactions.Where(item => item.WorkspaceId == workspaceId && item.TransferId == transaction.TransferId).ToListAsync(cancellationToken)
            : [transaction];
        foreach (var item in transactions)
        {
            if (item.CommitmentId is Guid commitmentId)
            {
                var commitment = await database.Commitments.SingleAsync(value => value.WorkspaceId == workspaceId && value.Id == commitmentId, cancellationToken);
                commitment.ReverseSettlement(item.Amount);
            }
            item.Void(voidedAt);
            var reconciled = await database.BankStatementEntries.Where(value => value.WorkspaceId == workspaceId && value.TransactionId == item.Id).ToListAsync(cancellationToken);
            foreach (var entry in reconciled)
            {
                entry.Reopen();
                database.ReconciliationHistoryEntries.Add(new ReconciliationHistoryEntry { Id = Guid.NewGuid(), WorkspaceId = workspaceId,
                    BankStatementEntryId = entry.Id, AccountId = entry.AccountId, UserId = userId, Action = "TransactionVoided",
                    TransactionId = item.Id, TransactionRemoved = true, Description = entry.Description, OccurredAt = voidedAt,
                    Details = "Conciliação reaberta após estorno da movimentação." });
            }
        }
        database.Transactions.UpdateRange(transactions);
        await database.SaveChangesAsync(cancellationToken);
        await unit.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<FinancialCommitment>> GetCommitmentsAsync(Guid workspaceId, CancellationToken cancellationToken)
        => await database.Commitments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).OrderBy(item => item.DueDate).ToListAsync(cancellationToken);

    public async Task AddCommitmentsAsync(IReadOnlyList<FinancialCommitment> commitments, CancellationToken cancellationToken)
    {
        database.Commitments.AddRange(commitments);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<FinancialCommitment> SettleCommitmentAsync(FinancialCommitment commitment, FinancialTransaction transaction, CancellationToken cancellationToken)
    {
        await using var unit = await database.Database.BeginTransactionAsync(cancellationToken);
        // Re-read inside the write transaction so stale requests cannot overwrite another settlement.
        var current = await database.Commitments.SingleAsync(item => item.WorkspaceId == commitment.WorkspaceId && item.Id == commitment.Id, cancellationToken);
        await database.Entry(current).ReloadAsync(cancellationToken);
        current.Settle(transaction.Amount);
        database.Transactions.Add(transaction);
        await database.SaveChangesAsync(cancellationToken);
        await unit.CommitAsync(cancellationToken);
        return current;
    }

    public async Task UpdateCommitmentAsync(FinancialCommitment commitment, CancellationToken cancellationToken)
    {
        database.Commitments.Update(commitment);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BankStatementEntry>> GetBankStatementEntriesAsync(Guid workspaceId, CancellationToken cancellationToken)
        => (await database.BankStatementEntries.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).ToListAsync(cancellationToken)).OrderByDescending(item => item.OccurredOn).ThenByDescending(item => item.ImportedAt).ToList();

    public async Task<IReadOnlyList<ReconciliationHistoryEntry>> GetReconciliationHistoryAsync(Guid workspaceId, CancellationToken cancellationToken)
        => (await database.ReconciliationHistoryEntries.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).ToListAsync(cancellationToken)).OrderByDescending(item => item.OccurredAt).ToList();

    public async Task<IReadOnlyList<ApplicationUser>> GetWorkspaceUsersAsync(Guid workspaceId, CancellationToken cancellationToken)
        => await (from user in database.Users.AsNoTracking() join member in database.WorkspaceMembers.AsNoTracking() on user.Id equals member.UserId where member.WorkspaceId == workspaceId select user).ToListAsync(cancellationToken);

    public async Task ImportBankStatementEntriesAsync(IReadOnlyList<BankStatementEntry> entries, IReadOnlyList<FinancialTransaction> transactions, IReadOnlyList<TransactionAllocation> allocations, IReadOnlyList<CategorizationRule> rules, IReadOnlyList<ReconciliationHistoryEntry> history, CancellationToken cancellationToken)
    {
        database.BankStatementEntries.AddRange(entries);
        database.Transactions.AddRange(transactions);
        database.TransactionAllocations.AddRange(allocations);
        database.CategorizationRules.AddRange(rules);
        database.ReconciliationHistoryEntries.AddRange(history);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateBankStatementEntryAsync(BankStatementEntry entry, ReconciliationHistoryEntry history, CancellationToken cancellationToken)
    {
        await using var unit = await database.Database.BeginTransactionAsync(cancellationToken);
        var current = await CurrentEntryAsync(entry, cancellationToken);
        if (history.Action == "Matched")
        {
            var transaction = await database.Transactions.AsNoTracking().SingleOrDefaultAsync(item => item.WorkspaceId == entry.WorkspaceId && item.Id == entry.TransactionId && !item.IsVoided, cancellationToken)
                ?? throw new InvalidOperationException("A movimentação não está mais disponível.");
            if (transaction.AccountId != current.AccountId || transaction.SignedAmount != current.SignedAmount ||
                await database.BankStatementEntries.AnyAsync(item => item.Id != current.Id && item.TransactionId == transaction.Id, cancellationToken))
                throw new InvalidOperationException("A movimentação não está disponível para esta conciliação.");
            current.Reconcile(transaction.Id);
        }
        else if (history.Action == "Ignored") current.Ignore();
        else if (history.Action == "Reopened")
        {
            if (current.Status == ReconciliationStatus.Reconciled) throw new InvalidOperationException("Desfaça a conciliação primeiro.");
            current.Reopen();
        }
        else throw new InvalidOperationException("Operação de conciliação inválida.");
        database.ReconciliationHistoryEntries.Add(history);
        await database.SaveChangesAsync(cancellationToken);
        await unit.CommitAsync(cancellationToken);
    }

    public async Task ReconcileWithNewTransactionAsync(BankStatementEntry entry, FinancialTransaction transaction, IReadOnlyList<TransactionAllocation> allocations, ReconciliationHistoryEntry history, CancellationToken cancellationToken)
    {
        await using var unit = await database.Database.BeginTransactionAsync(cancellationToken);
        var current = await CurrentEntryAsync(entry, cancellationToken);
        current.Reconcile(transaction.Id);
        database.Transactions.Add(transaction);
        database.TransactionAllocations.AddRange(allocations);
        database.ReconciliationHistoryEntries.Add(history);
        await database.SaveChangesAsync(cancellationToken);
        await unit.CommitAsync(cancellationToken);
    }

    public async Task UndoReconciliationAsync(BankStatementEntry entry, FinancialTransaction? transactionToVoid, ReconciliationHistoryEntry history, CancellationToken cancellationToken)
    {
        await using var unit = await database.Database.BeginTransactionAsync(cancellationToken);
        var current = await CurrentEntryAsync(entry, cancellationToken);
        if (current.Status != ReconciliationStatus.Reconciled || current.TransactionId != history.TransactionId)
            throw new InvalidOperationException("A conciliação já foi alterada. Atualize a tela.");
        current.Reopen();
        if (transactionToVoid is not null) database.Transactions.Update(transactionToVoid);
        database.ReconciliationHistoryEntries.Add(history);
        await database.SaveChangesAsync(cancellationToken);
        await unit.CommitAsync(cancellationToken);
    }

    private async Task<BankStatementEntry> CurrentEntryAsync(BankStatementEntry entry, CancellationToken ct)
    {
        var current = await database.BankStatementEntries.SingleAsync(item => item.WorkspaceId == entry.WorkspaceId && item.Id == entry.Id, ct);
        await database.Entry(current).ReloadAsync(ct);
        return current;
    }

    public async Task<IReadOnlyList<FinancialCategory>> GetCategoriesAsync(Guid workspaceId, CancellationToken cancellationToken)
        => await database.Categories.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public async Task AddCategoryAsync(FinancialCategory category, CancellationToken cancellationToken)
    {
        database.Categories.Add(category);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCategoryAsync(FinancialCategory category, CancellationToken cancellationToken)
    {
        database.Categories.Update(category);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategorizationRule>> GetCategorizationRulesAsync(Guid workspaceId, CancellationToken cancellationToken)
        => await database.CategorizationRules.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).OrderByDescending(item => item.Priority).ThenBy(item => item.Name).ToListAsync(cancellationToken);

    public async Task SaveCategorizationRuleAsync(CategorizationRule rule, bool isNew, CancellationToken cancellationToken)
    {
        if (isNew) database.CategorizationRules.Add(rule); else database.CategorizationRules.Update(rule);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteCategorizationRuleAsync(Guid workspaceId, Guid ruleId, CancellationToken cancellationToken)
    {
        var rule = await database.CategorizationRules.FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.Id == ruleId, cancellationToken);
        if (rule is null) return false;
        database.CategorizationRules.Remove(rule);
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<DocumentAttachment>> GetAttachmentsAsync(Guid workspaceId, CancellationToken cancellationToken)
        => (await database.DocumentAttachments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).ToListAsync(cancellationToken)).OrderByDescending(item => item.UploadedAt).ToList();

    public async Task AddAttachmentAsync(DocumentAttachment attachment, CancellationToken cancellationToken)
    {
        database.DocumentAttachments.Add(attachment);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<DocumentAttachment?> DeleteAttachmentAsync(Guid workspaceId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await database.DocumentAttachments.FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.Id == attachmentId, cancellationToken);
        if (attachment is null) return null;
        database.DocumentAttachments.Remove(attachment);
        await database.SaveChangesAsync(cancellationToken);
        return attachment;
    }

    public async Task<IReadOnlyList<CostCenter>> GetCostCentersAsync(Guid workspaceId, CancellationToken cancellationToken)
        => await database.CostCenters.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public async Task AddCostCenterAsync(CostCenter costCenter, CancellationToken cancellationToken)
    {
        database.CostCenters.Add(costCenter);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCostCenterAsync(CostCenter costCenter, CancellationToken cancellationToken)
    {
        database.CostCenters.Update(costCenter);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionAllocation>> GetAllocationsAsync(Guid workspaceId, CancellationToken cancellationToken)
        => await database.TransactionAllocations.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).ToListAsync(cancellationToken);

    public async Task ReplaceAllocationsAsync(Guid workspaceId, Guid transactionId, IReadOnlyList<TransactionAllocation> allocations, CancellationToken cancellationToken)
    {
        var current = await database.TransactionAllocations.Where(item => item.WorkspaceId == workspaceId && item.TransactionId == transactionId).ToListAsync(cancellationToken);
        database.TransactionAllocations.RemoveRange(current);
        database.TransactionAllocations.AddRange(allocations);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MonthlyBudget>> GetBudgetsAsync(Guid workspaceId, CancellationToken cancellationToken)
        => await database.MonthlyBudgets.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).ToListAsync(cancellationToken);

    public async Task SaveBudgetAsync(MonthlyBudget budget, bool isNew, CancellationToken cancellationToken)
    {
        if (isNew) database.MonthlyBudgets.Add(budget); else database.MonthlyBudgets.Update(budget);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteBudgetAsync(Guid workspaceId, Guid budgetId, CancellationToken cancellationToken)
    {
        var budget = await database.MonthlyBudgets.FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.Id == budgetId, cancellationToken);
        if (budget is null) return false;
        database.MonthlyBudgets.Remove(budget);
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<FinancialGoal>> GetGoalsAsync(Guid workspaceId, CancellationToken cancellationToken)
        => await database.FinancialGoals.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).OrderBy(item => item.DueDate).ToListAsync(cancellationToken);

    public async Task SaveGoalAsync(FinancialGoal goal, bool isNew, CancellationToken cancellationToken)
    {
        if (isNew) database.FinancialGoals.Add(goal); else database.FinancialGoals.Update(goal);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GoalContribution>> GetGoalContributionsAsync(Guid workspaceId, CancellationToken cancellationToken)
        => await database.GoalContributions.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).OrderByDescending(item => item.ContributedOn).ToListAsync(cancellationToken);

    public async Task AddGoalContributionAsync(GoalContribution contribution, CancellationToken cancellationToken)
    {
        database.GoalContributions.Add(contribution);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Contact>> GetContactsAsync(Guid workspaceId, CancellationToken cancellationToken)
        => await database.Contacts.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public async Task SaveContactAsync(Contact contact, bool isNew, CancellationToken cancellationToken)
    {
        if (isNew) database.Contacts.Add(contact); else database.Contacts.Update(contact);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetAuditEntriesAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var entries = await database.AuditEntries.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).ToListAsync(cancellationToken);
        return entries.OrderByDescending(item => item.OccurredAt).Take(500).ToList();
    }

    public async Task AddAuditEntryAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        database.AuditEntries.Add(entry);
        await database.SaveChangesAsync(cancellationToken);
    }
}
