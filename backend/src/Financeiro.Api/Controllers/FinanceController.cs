using Financeiro.Application;
using Financeiro.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Financeiro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class FinanceController(FinanceService service, IWebHostEnvironment environment, IWorkspaceContext workspaceContext) : ControllerBase
{
    [HttpGet("workspaces")]
    public async Task<ActionResult<IReadOnlyList<WorkspaceView>>> GetWorkspaces(CancellationToken cancellationToken)
        => Ok(await service.GetWorkspacesAsync(cancellationToken));

    [HttpPost("workspaces")]
    public async Task<ActionResult<WorkspaceView>> CreateWorkspace([FromBody] SaveWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<WorkspaceKind>(request.Kind, true, out var kind))
            return BadRequest(new ProblemDetails { Title = "Tipo de espaço inválido", Detail = "Use Personal ou Business." });
        try
        {
            var workspace = await service.CreateWorkspaceAsync(new SaveWorkspaceCommand(request.Name ?? "", kind), cancellationToken);
            return Created($"/api/workspaces/{workspace.Id}", workspace);
        }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Espaço inválido", Detail = exception.Message }); }
    }

    [HttpPut("workspaces/{workspaceId:guid}")]
    public async Task<ActionResult<WorkspaceView>> UpdateWorkspace(Guid workspaceId, [FromBody] SaveWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<WorkspaceKind>(request.Kind, true, out var kind))
            return BadRequest(new ProblemDetails { Title = "Tipo de espaço inválido", Detail = "Use Personal ou Business." });
        try { return Ok(await service.UpdateWorkspaceAsync(workspaceId, new SaveWorkspaceCommand(request.Name ?? "", kind), cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Espaço não encontrado", Detail = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Acesso negado", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Espaço inválido", Detail = exception.Message }); }
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardSummary>> GetDashboard(CancellationToken cancellationToken)
        => Ok(await service.GetDashboardAsync(cancellationToken));

    [HttpGet("accounts")]
    public async Task<ActionResult<IReadOnlyList<AccountView>>> GetAccounts([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await service.GetAccountsAsync(cancellationToken, includeInactive));

    [HttpPost("accounts")]
    public async Task<ActionResult<AccountView>> CreateAccount([FromBody] SaveAccountRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AccountType>(request.Type, true, out var type))
            return BadRequest(new ProblemDetails { Title = "Tipo de conta inválido", Detail = "Use Checking, Savings, Cash ou Investment." });
        try
        {
            var account = await service.CreateAccountAsync(new SaveAccountCommand(request.Name ?? "", request.Institution ?? "", request.LastFourDigits, type, request.OpeningBalance), cancellationToken);
            return Created($"/api/accounts/{account.Id}", account);
        }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Conta inválida", Detail = exception.Message }); }
    }

    [HttpPut("accounts/{accountId:guid}")]
    public async Task<ActionResult<AccountView>> UpdateAccount(Guid accountId, [FromBody] SaveAccountRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AccountType>(request.Type, true, out var type))
            return BadRequest(new ProblemDetails { Title = "Tipo de conta inválido", Detail = "Use Checking, Savings, Cash ou Investment." });
        try { return Ok(await service.UpdateAccountAsync(accountId, new SaveAccountCommand(request.Name ?? "", request.Institution ?? "", request.LastFourDigits, type, request.OpeningBalance), cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Conta não encontrada", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Conta inválida", Detail = exception.Message }); }
    }

    [HttpPost("accounts/{accountId:guid}/active")]
    public async Task<ActionResult<AccountView>> SetAccountActive(Guid accountId, [FromBody] SetActiveRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.SetAccountActiveAsync(accountId, request.Active, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Conta não encontrada", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível arquivar", Detail = exception.Message }); }
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<TransactionView>>> GetTransactions(CancellationToken cancellationToken)
        => Ok(await service.GetTransactionsAsync(cancellationToken));

    [HttpGet("cash-flow")]
    public async Task<ActionResult<CashFlowSummary>> GetCashFlow([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        try { return Ok(await service.GetCashFlowAsync(days, cancellationToken)); }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Período inválido", Detail = exception.Message });
        }
    }

    [HttpPost("transactions")]
    public async Task<ActionResult<TransactionView>> CreateTransaction([FromBody] CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TransactionType>(request.Type, true, out var type))
            return BadRequest(new ProblemDetails { Title = "Tipo de lançamento inválido", Detail = "Use Income ou Expense." });

        try
        {
            var transaction = await service.CreateTransactionAsync(
                new CreateTransactionCommand(request.AccountId, request.Description ?? "", request.Category ?? "", type, request.Amount, request.OccurredOn),
                cancellationToken);
            return Created($"/api/transactions/{transaction.Id}", transaction);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Não foi possível salvar o lançamento", Detail = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Conta não encontrada", Detail = exception.Message });
        }
    }

    [HttpPost("transfers")]
    public async Task<ActionResult<IReadOnlyList<TransactionView>>> CreateTransfer([FromBody] CreateTransferRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CreateTransferAsync(new CreateTransferCommand(request.SourceAccountId, request.DestinationAccountId, request.Description ?? "", request.Amount, request.OccurredOn), cancellationToken);
            return Created($"/api/transfers/{result[0].TransferId}", result);
        }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Transferência inválida", Detail = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Conta não encontrada", Detail = exception.Message }); }
    }

    [HttpPut("transactions/{transactionId:guid}")]
    public async Task<ActionResult<TransactionView>> UpdateTransaction(Guid transactionId, [FromBody] CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TransactionType>(request.Type, true, out var type))
            return BadRequest(new ProblemDetails { Title = "Tipo de lançamento inválido", Detail = "Use Income ou Expense." });
        try { return Ok(await service.UpdateTransactionAsync(transactionId, new CreateTransactionCommand(request.AccountId, request.Description ?? "", request.Category ?? "", type, request.Amount, request.OccurredOn), cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Registro não encontrado", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível editar o lançamento", Detail = exception.Message }); }
    }

    [HttpPut("transfers/{transferId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TransactionView>>> UpdateTransfer(Guid transferId, [FromBody] CreateTransferRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.UpdateTransferAsync(transferId, new CreateTransferCommand(request.SourceAccountId, request.DestinationAccountId, request.Description ?? "", request.Amount, request.OccurredOn), cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Transferência não encontrada", Detail = exception.Message }); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return BadRequest(new ProblemDetails { Title = "Não foi possível editar a transferência", Detail = exception.Message }); }
    }

    [HttpDelete("transactions/{transactionId:guid}")]
    public async Task<IActionResult> DeleteTransaction(Guid transactionId, CancellationToken cancellationToken)
        => await service.DeleteTransactionAsync(transactionId, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("commitments")]
    public async Task<ActionResult<IReadOnlyList<CommitmentView>>> GetCommitments(CancellationToken cancellationToken)
        => Ok(await service.GetCommitmentsAsync(cancellationToken));

    [HttpPost("commitments")]
    public async Task<ActionResult<IReadOnlyList<CommitmentView>>> CreateCommitment([FromBody] CreateCommitmentRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CommitmentKind>(request.Kind, true, out var kind))
            return BadRequest(new ProblemDetails { Title = "Tipo inválido", Detail = "Use Payable ou Receivable." });
        RecurrenceFrequency? recurrence = null;
        if (!string.IsNullOrWhiteSpace(request.RecurrenceFrequency))
        {
            if (!Enum.TryParse<RecurrenceFrequency>(request.RecurrenceFrequency, true, out var parsedRecurrence))
                return BadRequest(new ProblemDetails { Title = "Recorrência inválida", Detail = "Use Weekly, Monthly ou Yearly." });
            recurrence = parsedRecurrence;
        }
        try
        {
            var result = await service.CreateCommitmentsAsync(new CreateCommitmentCommand(request.ContactId, request.Description ?? "", request.Contact ?? "", request.Category ?? "", kind, request.TotalAmount, request.DueDate, request.CompetenceDate, request.Installments, recurrence, request.RecurrenceCount), cancellationToken);
            return Created("/api/commitments", result);
        }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Contato não encontrado", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível cadastrar", Detail = exception.Message }); }
    }

    [HttpPost("commitments/{commitmentId:guid}/settlements")]
    public async Task<ActionResult<CommitmentView>> SettleCommitment(Guid commitmentId, [FromBody] SettleCommitmentRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.SettleCommitmentAsync(commitmentId, new SettleCommitmentCommand(request.AccountId, request.Amount, request.OccurredOn), cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Registro não encontrado", Detail = exception.Message }); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return BadRequest(new ProblemDetails { Title = "Não foi possível realizar a baixa", Detail = exception.Message }); }
    }

    [HttpPut("commitments/{commitmentId:guid}")]
    public async Task<ActionResult<CommitmentView>> UpdateCommitment(Guid commitmentId, [FromBody] UpdateCommitmentRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CommitmentKind>(request.Kind, true, out var kind))
            return BadRequest(new ProblemDetails { Title = "Tipo inválido", Detail = "Use Payable ou Receivable." });
        try { return Ok(await service.UpdateCommitmentAsync(commitmentId, new UpdateCommitmentCommand(request.ContactId, request.Description ?? "", request.Contact ?? "", request.Category ?? "", kind, request.TotalAmount, request.DueDate, request.CompetenceDate), cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Compromisso não encontrado", Detail = exception.Message }); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return BadRequest(new ProblemDetails { Title = "Não foi possível editar o compromisso", Detail = exception.Message }); }
    }

    [HttpPost("commitments/{commitmentId:guid}/cancel")]
    public async Task<ActionResult<CommitmentView>> CancelCommitment(Guid commitmentId, CancellationToken cancellationToken)
    {
        try { return Ok(await service.CancelCommitmentAsync(commitmentId, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Registro não encontrado", Detail = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível cancelar", Detail = exception.Message }); }
    }

    [HttpGet("reconciliation")]
    public async Task<ActionResult<BankReconciliationSummary>> GetReconciliation([FromQuery] Guid? accountId, CancellationToken cancellationToken)
        => Ok(await service.GetReconciliationAsync(accountId, cancellationToken));

    [HttpPost("reconciliation/import")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<BankImportResult>> ImportStatement([FromForm] Guid accountId, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0) return BadRequest(new ProblemDetails { Title = "Arquivo vazio" });
        try
        {
            using var reader = new StreamReader(file.OpenReadStream(), detectEncodingFromByteOrderMarks: true);
            return Ok(await service.ImportBankStatementAsync(accountId, file.FileName, await reader.ReadToEndAsync(cancellationToken), cancellationToken));
        }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Conta não encontrada", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível importar", Detail = exception.Message }); }
    }

    [HttpPost("reconciliation/import/preview")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<BankImportPreview>> PreviewStatement([FromForm] Guid accountId, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0) return BadRequest(new ProblemDetails { Title = "Arquivo vazio" });
        try
        {
            using var reader = new StreamReader(file.OpenReadStream(), detectEncodingFromByteOrderMarks: true);
            return Ok(await service.PreviewBankStatementAsync(accountId, file.FileName, await reader.ReadToEndAsync(cancellationToken), cancellationToken));
        }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Conta não encontrada", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível analisar", Detail = exception.Message }); }
    }

    [HttpPost("reconciliation/import/confirm")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<BankImportConfirmation>> ConfirmStatement([FromForm] Guid accountId, [FromForm] IFormFile file, [FromForm] string decisions, CancellationToken cancellationToken)
    {
        if (file.Length == 0) return BadRequest(new ProblemDetails { Title = "Arquivo vazio" });
        try
        {
            var parsedDecisions = JsonSerializer.Deserialize<IReadOnlyList<BankImportDecision>>(decisions, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            using var reader = new StreamReader(file.OpenReadStream(), detectEncodingFromByteOrderMarks: true);
            return Ok(await service.ConfirmBankStatementAsync(accountId, file.FileName, await reader.ReadToEndAsync(cancellationToken), parsedDecisions, cancellationToken));
        }
        catch (JsonException) { return BadRequest(new ProblemDetails { Title = "Revisão inválida", Detail = "Não foi possível interpretar as decisões da importação." }); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Conta não encontrada", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível importar", Detail = exception.Message }); }
    }

    [HttpGet("reconciliation/history")]
    public async Task<ActionResult<IReadOnlyList<ReconciliationHistoryView>>> GetReconciliationHistory([FromQuery] Guid? accountId, CancellationToken cancellationToken)
        => Ok(await service.GetReconciliationHistoryAsync(accountId, cancellationToken));

    [HttpPost("reconciliation/{entryId:guid}/match")]
    public async Task<ActionResult<BankStatementEntryView>> MatchBankEntry(Guid entryId, [FromBody] MatchBankEntryRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.MatchBankEntryAsync(entryId, request.TransactionId, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Registro não encontrado", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Conciliação inválida", Detail = exception.Message }); }
    }

    [HttpPost("reconciliation/{entryId:guid}/create")]
    public async Task<ActionResult<BankStatementEntryView>> CreateFromBankEntry(Guid entryId, [FromBody] CreateFromBankEntryRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.CreateFromBankEntryAsync(entryId, new CreateFromBankEntryCommand(request.Category ?? ""), cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Registro não encontrado", Detail = exception.Message }); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return BadRequest(new ProblemDetails { Title = "Não foi possível criar o lançamento", Detail = exception.Message }); }
    }

    [HttpPost("reconciliation/{entryId:guid}/ignore")]
    public async Task<ActionResult<BankStatementEntryView>> IgnoreBankEntry(Guid entryId, CancellationToken cancellationToken)
    {
        try { return Ok(await service.SetBankEntryStatusAsync(entryId, true, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Registro não encontrado", Detail = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível ignorar", Detail = exception.Message }); }
    }

    [HttpPost("reconciliation/{entryId:guid}/reopen")]
    public async Task<ActionResult<BankStatementEntryView>> ReopenBankEntry(Guid entryId, CancellationToken cancellationToken)
    {
        try { return Ok(await service.SetBankEntryStatusAsync(entryId, false, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Registro não encontrado", Detail = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível reabrir", Detail = exception.Message }); }
    }

    [HttpPost("reconciliation/{entryId:guid}/undo")]
    public async Task<ActionResult<BankStatementEntryView>> UndoReconciliation(Guid entryId, [FromBody] UndoReconciliationRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.UndoReconciliationAsync(entryId, request.RemoveCreatedTransaction, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Registro não encontrado", Detail = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível desfazer", Detail = exception.Message }); }
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryView>>> GetCategories(CancellationToken cancellationToken)
        => Ok(await service.GetCategoriesAsync(cancellationToken));

    [HttpPost("categories")]
    public async Task<ActionResult<CategoryView>> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CategoryKind>(request.Kind, true, out var kind)) return BadRequest(new ProblemDetails { Title = "Tipo inválido", Detail = "Use Income, Expense ou Both." });
        try { var result = await service.CreateCategoryAsync(new CreateCategoryCommand(request.Name ?? "", kind, request.ParentId), cancellationToken); return Created($"/api/categories/{result.Id}", result); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Categoria principal não encontrada", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível cadastrar", Detail = exception.Message }); }
    }

    [HttpPost("categories/{categoryId:guid}/active")]
    public async Task<ActionResult<CategoryView>> SetCategoryActive(Guid categoryId, [FromBody] SetActiveRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.SetCategoryActiveAsync(categoryId, request.Active, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Categoria não encontrada", Detail = exception.Message }); }
    }

    [HttpGet("cost-centers")]
    public async Task<ActionResult<IReadOnlyList<CostCenterView>>> GetCostCenters(CancellationToken cancellationToken)
        => Ok(await service.GetCostCentersAsync(cancellationToken));

    [HttpPost("cost-centers")]
    public async Task<ActionResult<CostCenterView>> CreateCostCenter([FromBody] CreateCostCenterRequest request, CancellationToken cancellationToken)
    {
        try { var result = await service.CreateCostCenterAsync(new CreateCostCenterCommand(request.Name ?? "", request.Code ?? ""), cancellationToken); return Created($"/api/cost-centers/{result.Id}", result); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível cadastrar", Detail = exception.Message }); }
    }

    [HttpPost("cost-centers/{costCenterId:guid}/active")]
    public async Task<ActionResult<CostCenterView>> SetCostCenterActive(Guid costCenterId, [FromBody] SetActiveRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.SetCostCenterActiveAsync(costCenterId, request.Active, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Centro de custo não encontrado", Detail = exception.Message }); }
    }

    [HttpGet("allocations")]
    public async Task<ActionResult<IReadOnlyList<TransactionAllocationView>>> GetAllocations([FromQuery] Guid? transactionId, CancellationToken cancellationToken)
        => Ok(await service.GetAllocationsAsync(transactionId, cancellationToken));

    [HttpPut("transactions/{transactionId:guid}/allocations")]
    public async Task<ActionResult<IReadOnlyList<TransactionAllocationView>>> ReplaceAllocations(Guid transactionId, [FromBody] IReadOnlyList<AllocationItemRequest> request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.ReplaceAllocationsAsync(transactionId, request.Select(item => new AllocationItemCommand(item.CostCenterId, item.Amount)).ToList(), cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Lançamento não encontrado", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Rateio inválido", Detail = exception.Message }); }
    }

    [HttpGet("budgets")]
    public async Task<ActionResult<MonthlyBudgetSummary>> GetBudgets([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    {
        try { return Ok(await service.GetBudgetSummaryAsync(year, month, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Período inválido", Detail = exception.Message }); }
    }

    [HttpPut("budgets")]
    public async Task<ActionResult<BudgetLineView>> SaveBudget([FromBody] SaveBudgetRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<BudgetTargetKind>(request.TargetKind, true, out var targetKind)) return BadRequest(new ProblemDetails { Title = "Tipo inválido", Detail = "Use Category ou CostCenter." });
        try { return Ok(await service.SaveBudgetAsync(new SaveBudgetCommand(request.Year, request.Month, targetKind, request.TargetId, request.PlannedAmount, request.AlertPercentage), cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Destino não encontrado", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Orçamento inválido", Detail = exception.Message }); }
    }

    [HttpDelete("budgets/{budgetId:guid}")]
    public async Task<IActionResult> DeleteBudget(Guid budgetId, CancellationToken cancellationToken)
        => await service.DeleteBudgetAsync(budgetId, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("reports/dre")]
    public async Task<ActionResult<DreSummary>> GetDre([FromQuery] int year, [FromQuery] int month, [FromQuery] Guid? costCenterId, CancellationToken cancellationToken)
    {
        try { return Ok(await service.GetDreAsync(year, month, costCenterId, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Centro de custo não encontrado", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Período inválido", Detail = exception.Message }); }
    }

    [HttpGet("reports/budget-variance")]
    public async Task<ActionResult<BudgetVarianceReport>> GetBudgetVariance([FromQuery] int year, [FromQuery] int month, [FromQuery] int months = 6, CancellationToken cancellationToken = default)
    {
        try { return Ok(await service.GetBudgetVarianceAsync(year, month, months, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Parâmetros inválidos", Detail = exception.Message }); }
    }

    [HttpGet("goals")]
    public async Task<ActionResult<FinancialGoalsSummary>> GetGoals(CancellationToken cancellationToken)
        => Ok(await service.GetGoalsAsync(cancellationToken));

    [HttpPost("goals")]
    public async Task<ActionResult<FinancialGoalView>> CreateGoal([FromBody] SaveGoalRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FinancialGoalKind>(request.Kind, true, out var kind)) return BadRequest(new ProblemDetails { Title = "Tipo inválido", Detail = "Use Revenue, ExpenseLimit, EmergencyReserve, CashBalance ou Custom." });
        try { var result = await service.CreateGoalAsync(new SaveGoalCommand(request.Name ?? "", kind, request.TargetAmount, request.StartDate, request.DueDate, request.Category), cancellationToken); return Created($"/api/goals/{result.Id}", result); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Categoria não encontrada", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Meta inválida", Detail = exception.Message }); }
    }

    [HttpPut("goals/{goalId:guid}")]
    public async Task<ActionResult<FinancialGoalView>> UpdateGoal(Guid goalId, [FromBody] SaveGoalRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FinancialGoalKind>(request.Kind, true, out var kind)) return BadRequest(new ProblemDetails { Title = "Tipo inválido" });
        try { return Ok(await service.UpdateGoalAsync(goalId, new SaveGoalCommand(request.Name ?? "", kind, request.TargetAmount, request.StartDate, request.DueDate, request.Category), cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Registro não encontrado", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Meta inválida", Detail = exception.Message }); }
    }

    [HttpPost("goals/{goalId:guid}/active")]
    public async Task<ActionResult<FinancialGoalView>> SetGoalActive(Guid goalId, [FromBody] SetActiveRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.SetGoalActiveAsync(goalId, request.Active, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Meta não encontrada", Detail = exception.Message }); }
    }

    [HttpPost("goals/{goalId:guid}/contributions")]
    public async Task<ActionResult<FinancialGoalView>> AddGoalContribution(Guid goalId, [FromBody] AddGoalContributionRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.AddGoalContributionAsync(goalId, new AddGoalContributionCommand(request.Amount, request.ContributedOn, request.Note ?? ""), cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Meta não encontrada", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Aporte inválido", Detail = exception.Message }); }
    }

    [HttpGet("contacts")]
    public async Task<ActionResult<IReadOnlyList<ContactView>>> GetContacts([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await service.GetContactsAsync(includeInactive, cancellationToken));

    [HttpPost("contacts")]
    public async Task<ActionResult<ContactView>> CreateContact([FromBody] SaveContactRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ContactKind>(request.Kind, true, out var kind)) return BadRequest(new ProblemDetails { Title = "Tipo inválido", Detail = "Use Customer, Supplier ou Both." });
        try { var result = await service.CreateContactAsync(new SaveContactCommand(request.Name ?? "", kind, request.TaxId, request.Email, request.Phone), cancellationToken); return Created($"/api/contacts/{result.Id}", result); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Contato inválido", Detail = exception.Message }); }
    }

    [HttpPut("contacts/{contactId:guid}")]
    public async Task<ActionResult<ContactView>> UpdateContact(Guid contactId, [FromBody] SaveContactRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ContactKind>(request.Kind, true, out var kind)) return BadRequest(new ProblemDetails { Title = "Tipo inválido" });
        try { return Ok(await service.UpdateContactAsync(contactId, new SaveContactCommand(request.Name ?? "", kind, request.TaxId, request.Email, request.Phone), cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Contato não encontrado", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Contato inválido", Detail = exception.Message }); }
    }

    [HttpPost("contacts/{contactId:guid}/active")]
    public async Task<ActionResult<ContactView>> SetContactActive(Guid contactId, [FromBody] SetActiveRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.SetContactActiveAsync(contactId, request.Active, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Contato não encontrado", Detail = exception.Message }); }
    }

    [HttpGet("categorization-rules")]
    public async Task<ActionResult<IReadOnlyList<CategorizationRuleView>>> GetCategorizationRules(CancellationToken cancellationToken)
        => Ok(await service.GetCategorizationRulesAsync(cancellationToken));

    [HttpGet("attachments")]
    public async Task<ActionResult<IReadOnlyList<AttachmentView>>> GetAttachments([FromQuery] string entityType, [FromQuery] Guid entityId, CancellationToken cancellationToken)
    {
        try { return Ok(await service.GetAttachmentsAsync(entityType, entityId, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Tipo de anexo inválido", Detail = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Registro não encontrado", Detail = exception.Message }); }
    }

    [HttpGet("attachments/counts")]
    public async Task<ActionResult<IReadOnlyDictionary<Guid, int>>> GetAttachmentCounts([FromQuery] string entityType, CancellationToken cancellationToken)
    {
        try { return Ok(await service.GetAttachmentCountsAsync(entityType, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Tipo de anexo inválido", Detail = exception.Message }); }
    }

    [HttpPost("attachments")]
    [RequestSizeLimit(10_485_760)]
    public async Task<ActionResult<AttachmentView>> UploadAttachment([FromForm] string entityType, [FromForm] Guid entityId, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > 10_000_000) return BadRequest(new ProblemDetails { Title = "Arquivo inválido", Detail = "Envie um arquivo de até 10 MB." });
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var contentType = extension switch { ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => "" };
        if (contentType.Length == 0) return BadRequest(new ProblemDetails { Title = "Formato não permitido", Detail = "Use arquivos PDF, PNG, JPG ou JPEG." });
        await using (var inspection = file.OpenReadStream())
        {
            var header = new byte[8]; var read = await inspection.ReadAsync(header, cancellationToken);
            var valid = extension == ".pdf" ? read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8) : extension == ".png" ? read >= 8 && header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) : read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
            if (!valid) return BadRequest(new ProblemDetails { Title = "Conteúdo inválido", Detail = "O conteúdo do arquivo não corresponde ao formato informado." });
        }
        var originalName = Path.GetFileName(file.FileName); if (originalName.Length > 180) originalName = originalName[^180..];
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var directory = Path.Combine(environment.ContentRootPath, "App_Data", "Attachments", workspaceContext.WorkspaceId.ToString("N"));
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, storedName);
        try
        {
            await using (var output = System.IO.File.Create(path)) await file.CopyToAsync(output, cancellationToken);
            var result = await service.CreateAttachmentAsync(entityType, entityId, originalName, storedName, contentType, file.Length, cancellationToken);
            return Created($"/api/attachments/{result.Id}", result);
        }
        catch { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); throw; }
    }

    [HttpGet("attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, CancellationToken cancellationToken)
    {
        try
        {
            var attachment = await service.GetAttachmentFileAsync(attachmentId, cancellationToken);
            var path = Path.Combine(environment.ContentRootPath, "App_Data", "Attachments", workspaceContext.WorkspaceId.ToString("N"), attachment.StoredFileName);
            if (!System.IO.File.Exists(path)) return NotFound(new ProblemDetails { Title = "Arquivo não encontrado" });
            return PhysicalFile(path, attachment.ContentType, attachment.OriginalFileName, enableRangeProcessing: true);
        }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Anexo não encontrado", Detail = exception.Message }); }
    }

    [HttpDelete("attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId, CancellationToken cancellationToken)
    {
        try
        {
            var attachment = await service.DeleteAttachmentAsync(attachmentId, cancellationToken);
            var path = Path.Combine(environment.ContentRootPath, "App_Data", "Attachments", workspaceContext.WorkspaceId.ToString("N"), attachment.StoredFileName);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Anexo não encontrado", Detail = exception.Message }); }
    }

    [HttpPost("categorization-rules")]
    public async Task<ActionResult<CategorizationRuleView>> CreateCategorizationRule([FromBody] SaveCategorizationRuleRequest request, CancellationToken cancellationToken)
    {
        if (!TryCategorizationCommand(request, out var command, out var problem)) return BadRequest(problem);
        try { var result = await service.CreateCategorizationRuleAsync(command!, cancellationToken); return Created($"/api/categorization-rules/{result.Id}", result); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Referência não encontrada", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Regra inválida", Detail = exception.Message }); }
    }

    [HttpPut("categorization-rules/{ruleId:guid}")]
    public async Task<ActionResult<CategorizationRuleView>> UpdateCategorizationRule(Guid ruleId, [FromBody] SaveCategorizationRuleRequest request, CancellationToken cancellationToken)
    {
        if (!TryCategorizationCommand(request, out var command, out var problem)) return BadRequest(problem);
        try { return Ok(await service.UpdateCategorizationRuleAsync(ruleId, command!, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Regra não encontrada", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Regra inválida", Detail = exception.Message }); }
    }

    [HttpDelete("categorization-rules/{ruleId:guid}")]
    public async Task<IActionResult> DeleteCategorizationRule(Guid ruleId, CancellationToken cancellationToken)
        => await service.DeleteCategorizationRuleAsync(ruleId, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("categorization-rules/{ruleId:guid}/preview")]
    public async Task<ActionResult<CategorizationRulePreview>> PreviewCategorizationRule(Guid ruleId, CancellationToken cancellationToken)
    {
        try { return Ok(await service.PreviewCategorizationRuleAsync(ruleId, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Regra não encontrada", Detail = exception.Message }); }
    }

    [HttpPost("categorization-rules/{ruleId:guid}/apply")]
    public async Task<ActionResult<object>> ApplyCategorizationRule(Guid ruleId, CancellationToken cancellationToken)
    {
        try { return Ok(new { updated = await service.ApplyCategorizationRuleAsync(ruleId, cancellationToken) }); }
        catch (KeyNotFoundException exception) { return NotFound(new ProblemDetails { Title = "Regra não encontrada", Detail = exception.Message }); }
    }

    [HttpGet("audit")]
    public async Task<ActionResult<IReadOnlyList<AuditEntryView>>> GetAudit(CancellationToken cancellationToken)
        => Ok(await service.GetAuditEntriesAsync(cancellationToken));

    private static bool TryCategorizationCommand(SaveCategorizationRuleRequest request, out SaveCategorizationRuleCommand? command, out ProblemDetails? problem)
    {
        command = null; problem = null;
        if (!Enum.TryParse<DescriptionMatchMode>(request.MatchMode, true, out var matchMode)) { problem = new ProblemDetails { Title = "Operador inválido", Detail = "Use Contains, StartsWith ou Equals." }; return false; }
        TransactionType? transactionType = null;
        if (!string.IsNullOrWhiteSpace(request.TransactionType))
        {
            if (!Enum.TryParse<TransactionType>(request.TransactionType, true, out var parsed)) { problem = new ProblemDetails { Title = "Tipo inválido", Detail = "Use Income ou Expense." }; return false; }
            transactionType = parsed;
        }
        command = new SaveCategorizationRuleCommand(request.Name ?? "", request.DescriptionPattern ?? "", matchMode, transactionType, request.AccountId, request.Category ?? "", request.CostCenterId, request.Priority, request.IsActive);
        return true;
    }
}

public sealed record CreateTransactionRequest(Guid AccountId, string? Description, string? Category, string? Type, decimal Amount, DateOnly OccurredOn);
public sealed record CreateTransferRequest(Guid SourceAccountId, Guid DestinationAccountId, string? Description, decimal Amount, DateOnly OccurredOn);
public sealed record SaveWorkspaceRequest(string? Name, string? Kind);
public sealed record SaveAccountRequest(string? Name, string? Institution, string? LastFourDigits, string? Type, decimal OpeningBalance);
public sealed record CreateCommitmentRequest(Guid? ContactId, string? Description, string? Contact, string? Category, string? Kind, decimal TotalAmount, DateOnly DueDate, DateOnly CompetenceDate, int Installments = 1, string? RecurrenceFrequency = null, int RecurrenceCount = 1);
public sealed record UpdateCommitmentRequest(Guid? ContactId, string? Description, string? Contact, string? Category, string? Kind, decimal TotalAmount, DateOnly DueDate, DateOnly CompetenceDate);
public sealed record SettleCommitmentRequest(Guid AccountId, decimal Amount, DateOnly OccurredOn);
public sealed record MatchBankEntryRequest(Guid TransactionId);
public sealed record CreateFromBankEntryRequest(string? Category);
public sealed record UndoReconciliationRequest(bool RemoveCreatedTransaction);
public sealed record CreateCategoryRequest(string? Name, string? Kind, Guid? ParentId);
public sealed record CreateCostCenterRequest(string? Name, string? Code);
public sealed record SetActiveRequest(bool Active);
public sealed record AllocationItemRequest(Guid CostCenterId, decimal Amount);
public sealed record SaveBudgetRequest(int Year, int Month, string? TargetKind, Guid TargetId, decimal PlannedAmount, int AlertPercentage = 80);
public sealed record SaveGoalRequest(string? Name, string? Kind, decimal TargetAmount, DateOnly StartDate, DateOnly DueDate, string? Category);
public sealed record AddGoalContributionRequest(decimal Amount, DateOnly ContributedOn, string? Note);
public sealed record SaveContactRequest(string? Name, string? Kind, string? TaxId, string? Email, string? Phone);
public sealed record SaveCategorizationRuleRequest(string? Name, string? DescriptionPattern, string? MatchMode, string? TransactionType, Guid? AccountId, string? Category, Guid? CostCenterId, int Priority = 100, bool IsActive = true);
