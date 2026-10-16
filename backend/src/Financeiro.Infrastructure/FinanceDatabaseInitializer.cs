using Financeiro.Application;
using Financeiro.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Financeiro.Infrastructure;

public static class FinanceDatabaseInitializer
{
    public static async Task InitializeFinanceDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
        var workspaceId = FinanceService.SeedWorkspaceId;
        var primaryId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var reserveId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var cashId = Guid.Parse("20000000-0000-0000-0000-000000000003");
        var today = FinancialCalendar.Today(TimeProvider.System);

        if (!await database.Workspaces.AnyAsync(cancellationToken))
        {
            database.Workspaces.Add(new Workspace { Id = workspaceId, Name = "Studio Norte", Kind = WorkspaceKind.Business, CreatedAt = DateTimeOffset.UtcNow });
            await database.SaveChangesAsync(cancellationToken);
        }

        if (!await database.Accounts.AnyAsync(cancellationToken))
        {
            database.Accounts.AddRange(
                new FinancialAccount { Id = primaryId, WorkspaceId = workspaceId, Name = "Conta principal", Institution = "Banco Inter", LastFourDigits = "4581", Type = AccountType.Checking, OpeningBalance = 28739.02m },
                new FinancialAccount { Id = reserveId, WorkspaceId = workspaceId, Name = "Reserva", Institution = "Liquidez diária", Type = AccountType.Investment, OpeningBalance = 13200m },
                new FinancialAccount { Id = cashId, WorkspaceId = workspaceId, Name = "Caixa", Institution = "Dinheiro físico", Type = AccountType.Cash, OpeningBalance = 3650m });
            database.Transactions.AddRange(
                Entry(primaryId, "Assinatura Figma", "Software", TransactionType.Expense, 96m, today),
                Entry(primaryId, "Studio Aurora", "Receita de serviços", TransactionType.Income, 4850m, today),
                Entry(primaryId, "Google Workspace", "Software", TransactionType.Expense, 81.60m, today.AddDays(-1)),
                Entry(primaryId, "Coworking Central", "Escritório", TransactionType.Expense, 690m, today.AddDays(-1)),
                Entry(primaryId, "Transferência para reserva", "Reserva financeira", TransactionType.Expense, 1200m, today.AddDays(-2)),
                Entry(reserveId, "Aporte da conta principal", "Reserva financeira", TransactionType.Income, 1200m, today.AddDays(-2)),
                Entry(primaryId, "Projeto Horizonte", "Receita de serviços", TransactionType.Income, 8200m, today.AddDays(-5)),
                Entry(primaryId, "Internet empresarial", "Utilidades", TransactionType.Expense, 189.90m, today.AddDays(-7)));
        }

        if (!await database.Commitments.AnyAsync(cancellationToken))
        {
            database.Commitments.AddRange(
                Commitment("Aluguel do escritório", "Imobiliária Centro", "Escritório", CommitmentKind.Payable, 2350m, today.AddDays(-2)),
                Commitment("Impostos — DAS", "Receita Federal", "Impostos", CommitmentKind.Payable, 742.80m, today.AddDays(2)),
                Commitment("Projeto Agência Lume", "Agência Lume", "Receita de serviços", CommitmentKind.Receivable, 6200m, today.AddDays(5)),
                Commitment("Hospedagem anual", "Cloudhost", "Tecnologia", CommitmentKind.Payable, 580m, today.AddDays(12)));
        }

        if (!await database.Categories.AnyAsync(cancellationToken))
        {
            var revenueId = Guid.Parse("30000000-0000-0000-0000-000000000001");
            var operationsId = Guid.Parse("30000000-0000-0000-0000-000000000002");
            database.Categories.AddRange(
                Category(revenueId, "Receitas", CategoryKind.Income),
                Category(Guid.Parse("30000000-0000-0000-0000-000000000003"), "Receita de serviços", CategoryKind.Income, revenueId),
                Category(operationsId, "Despesas operacionais", CategoryKind.Expense),
                Category(Guid.Parse("30000000-0000-0000-0000-000000000004"), "Software", CategoryKind.Expense, operationsId),
                Category(Guid.Parse("30000000-0000-0000-0000-000000000005"), "Escritório", CategoryKind.Expense, operationsId),
                Category(Guid.Parse("30000000-0000-0000-0000-000000000006"), "Impostos", CategoryKind.Expense),
                Category(Guid.Parse("30000000-0000-0000-0000-000000000007"), "Reserva financeira", CategoryKind.Both));
        }

        if (!await database.CostCenters.AnyAsync(cancellationToken))
        {
            database.CostCenters.AddRange(
                Center("40000000-0000-0000-0000-000000000001", "Administrativo", "ADM"),
                Center("40000000-0000-0000-0000-000000000002", "Comercial", "COM"),
                Center("40000000-0000-0000-0000-000000000003", "Projetos de clientes", "PROJ"));
        }

        if (!await database.MonthlyBudgets.AnyAsync(cancellationToken))
        {
            database.MonthlyBudgets.AddRange(
                Budget("50000000-0000-0000-0000-000000000001", today, BudgetTargetKind.Category, "30000000-0000-0000-0000-000000000004", 2500m),
                Budget("50000000-0000-0000-0000-000000000002", today, BudgetTargetKind.Category, "30000000-0000-0000-0000-000000000005", 4200m),
                Budget("50000000-0000-0000-0000-000000000003", today, BudgetTargetKind.CostCenter, "40000000-0000-0000-0000-000000000001", 6000m));
        }

        if (!await database.FinancialGoals.AnyAsync(cancellationToken))
        {
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var reserveGoalId = Guid.Parse("60000000-0000-0000-0000-000000000003");
            database.FinancialGoals.AddRange(
                Goal("60000000-0000-0000-0000-000000000001", "Faturamento mensal", FinancialGoalKind.Revenue, 30000m, monthStart, monthEnd),
                Goal("60000000-0000-0000-0000-000000000002", "Limite de despesas", FinancialGoalKind.ExpenseLimit, 7000m, monthStart, monthEnd),
                Goal(reserveGoalId.ToString(), "Reserva de emergência", FinancialGoalKind.EmergencyReserve, 30000m, today, today.AddYears(1)));
            database.GoalContributions.Add(new GoalContribution { Id = Guid.Parse("70000000-0000-0000-0000-000000000001"), WorkspaceId = FinanceService.SeedWorkspaceId, GoalId = reserveGoalId, Amount = 13200m, ContributedOn = today, Note = "Saldo inicial da reserva", CreatedAt = DateTimeOffset.UtcNow });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private static FinancialTransaction Entry(Guid accountId, string description, string category, TransactionType type, decimal amount, DateOnly date)
        => new() { Id = Guid.NewGuid(), WorkspaceId = FinanceService.SeedWorkspaceId, AccountId = accountId, Description = description, Category = category, Type = type, Amount = amount, OccurredOn = date, CreatedAt = DateTimeOffset.UtcNow };
    private static FinancialCommitment Commitment(string description, string contact, string category, CommitmentKind kind, decimal amount, DateOnly dueDate)
        => new() { Id = Guid.NewGuid(), SeriesId = Guid.NewGuid(), WorkspaceId = FinanceService.SeedWorkspaceId, Description = description, Contact = contact, Category = category, Kind = kind, TotalAmount = amount, DueDate = dueDate, CompetenceDate = dueDate, CreatedAt = DateTimeOffset.UtcNow };
    private static FinancialCategory Category(Guid id, string name, CategoryKind kind, Guid? parentId = null)
        => new() { Id = id, WorkspaceId = FinanceService.SeedWorkspaceId, Name = name, Kind = kind, ParentId = parentId, CreatedAt = DateTimeOffset.UtcNow };
    private static CostCenter Center(string id, string name, string code)
        => new() { Id = Guid.Parse(id), WorkspaceId = FinanceService.SeedWorkspaceId, Name = name, Code = code, CreatedAt = DateTimeOffset.UtcNow };
    private static MonthlyBudget Budget(string id, DateOnly period, BudgetTargetKind targetKind, string targetId, decimal amount)
    {
        var budget = new MonthlyBudget { Id = Guid.Parse(id), WorkspaceId = FinanceService.SeedWorkspaceId, Year = period.Year, Month = period.Month, TargetKind = targetKind, TargetId = Guid.Parse(targetId), CreatedAt = DateTimeOffset.UtcNow };
        budget.Update(amount, 80);
        return budget;
    }
    private static FinancialGoal Goal(string id, string name, FinancialGoalKind kind, decimal amount, DateOnly startDate, DateOnly dueDate)
    {
        var goal = new FinancialGoal { Id = Guid.Parse(id), WorkspaceId = FinanceService.SeedWorkspaceId, CreatedAt = DateTimeOffset.UtcNow };
        goal.Update(name, kind, amount, startDate, dueDate, null);
        return goal;
    }
}
