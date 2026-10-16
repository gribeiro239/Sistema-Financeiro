using Financeiro.Domain;

namespace Financeiro.Application;

public interface IFinanceRepository
{
    Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkspaceMember>> GetWorkspaceMembershipsAsync(Guid userId, CancellationToken cancellationToken);
    Task SaveWorkspaceAsync(Workspace workspace, bool isNew, CancellationToken cancellationToken);
    Task SaveWorkspaceWithOwnerAsync(Workspace workspace, WorkspaceMember owner, CancellationToken cancellationToken);
    Task<IReadOnlyList<FinancialAccount>> GetAccountsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task SaveAccountAsync(FinancialAccount account, bool isNew, CancellationToken cancellationToken);
    Task<IReadOnlyList<FinancialTransaction>> GetTransactionsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task AddTransactionAsync(FinancialTransaction transaction, CancellationToken cancellationToken);
    Task AddTransactionsAsync(IReadOnlyList<FinancialTransaction> transactions, CancellationToken cancellationToken);
    Task UpdateTransactionsAsync(IReadOnlyList<FinancialTransaction> transactions, CancellationToken cancellationToken);
    Task<bool> VoidTransactionAsync(Guid workspaceId, Guid transactionId, Guid userId, DateTimeOffset voidedAt, CancellationToken cancellationToken);
    Task<IReadOnlyList<FinancialCommitment>> GetCommitmentsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task AddCommitmentsAsync(IReadOnlyList<FinancialCommitment> commitments, CancellationToken cancellationToken);
    Task<FinancialCommitment> SettleCommitmentAsync(FinancialCommitment commitment, FinancialTransaction transaction, CancellationToken cancellationToken);
    Task UpdateCommitmentAsync(FinancialCommitment commitment, CancellationToken cancellationToken);
    Task<IReadOnlyList<BankStatementEntry>> GetBankStatementEntriesAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReconciliationHistoryEntry>> GetReconciliationHistoryAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApplicationUser>> GetWorkspaceUsersAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task ImportBankStatementEntriesAsync(IReadOnlyList<BankStatementEntry> entries, IReadOnlyList<FinancialTransaction> transactions, IReadOnlyList<TransactionAllocation> allocations, IReadOnlyList<CategorizationRule> rules, IReadOnlyList<ReconciliationHistoryEntry> history, CancellationToken cancellationToken);
    Task UpdateBankStatementEntryAsync(BankStatementEntry entry, ReconciliationHistoryEntry history, CancellationToken cancellationToken);
    Task ReconcileWithNewTransactionAsync(BankStatementEntry entry, FinancialTransaction transaction, IReadOnlyList<TransactionAllocation> allocations, ReconciliationHistoryEntry history, CancellationToken cancellationToken);
    Task UndoReconciliationAsync(BankStatementEntry entry, FinancialTransaction? transactionToVoid, ReconciliationHistoryEntry history, CancellationToken cancellationToken);
    Task<IReadOnlyList<FinancialCategory>> GetCategoriesAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task AddCategoryAsync(FinancialCategory category, CancellationToken cancellationToken);
    Task UpdateCategoryAsync(FinancialCategory category, CancellationToken cancellationToken);
    Task<IReadOnlyList<CategorizationRule>> GetCategorizationRulesAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task SaveCategorizationRuleAsync(CategorizationRule rule, bool isNew, CancellationToken cancellationToken);
    Task<bool> DeleteCategorizationRuleAsync(Guid workspaceId, Guid ruleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentAttachment>> GetAttachmentsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task AddAttachmentAsync(DocumentAttachment attachment, CancellationToken cancellationToken);
    Task<DocumentAttachment?> DeleteAttachmentAsync(Guid workspaceId, Guid attachmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CostCenter>> GetCostCentersAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task AddCostCenterAsync(CostCenter costCenter, CancellationToken cancellationToken);
    Task UpdateCostCenterAsync(CostCenter costCenter, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransactionAllocation>> GetAllocationsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task ReplaceAllocationsAsync(Guid workspaceId, Guid transactionId, IReadOnlyList<TransactionAllocation> allocations, CancellationToken cancellationToken);
    Task<IReadOnlyList<MonthlyBudget>> GetBudgetsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task SaveBudgetAsync(MonthlyBudget budget, bool isNew, CancellationToken cancellationToken);
    Task<bool> DeleteBudgetAsync(Guid workspaceId, Guid budgetId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FinancialGoal>> GetGoalsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task SaveGoalAsync(FinancialGoal goal, bool isNew, CancellationToken cancellationToken);
    Task<IReadOnlyList<GoalContribution>> GetGoalContributionsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task AddGoalContributionAsync(GoalContribution contribution, CancellationToken cancellationToken);
    Task<IReadOnlyList<Contact>> GetContactsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task SaveContactAsync(Contact contact, bool isNew, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditEntry>> GetAuditEntriesAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task AddAuditEntryAsync(AuditEntry entry, CancellationToken cancellationToken);
}
