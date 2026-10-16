using Financeiro.Domain;
using Financeiro.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Api;

public sealed record HelpArticleView(string Id, string Title, string Summary, string Category, string Module, IReadOnlyList<string> Keywords, IReadOnlyList<string> Steps);
public sealed record SupportTicketMessageView(Guid Id, Guid AuthorUserId, string AuthorName, string Body, DateTimeOffset CreatedAt);
public sealed record SupportTicketView(Guid Id, Guid WorkspaceId, string WorkspaceName, Guid RequesterUserId, string RequesterName, string Target, string Subject, string Category, string Priority, string Status, bool ConsentToShareWorkspaceData, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<SupportTicketMessageView> Messages);
public sealed record CreateSupportTicketCommand(SupportTicketTarget Target, string Subject, string Category, SupportTicketPriority Priority, string Description, bool ConsentToShareWorkspaceData);

public sealed class HelpCenterService(FinanceDbContext database)
{
    private static readonly IReadOnlyList<HelpArticleView> Articles =
    [
        new("primeiros-passos", "Primeiros passos no Nexo", "Configure seu espaço, contas e primeiro lançamento.", "Começando", "hoje", ["início", "configurar", "conta"], ["Crie ou selecione um espaço financeiro.", "Cadastre suas contas com o saldo inicial correto.", "Registre uma receita ou despesa.", "Confira o saldo na página Hoje."]),
        new("importar-extrato", "Como importar um extrato OFX ou CSV", "Envie um extrato com pré-visualização e validação.", "Conciliação", "conciliacao", ["ofx", "csv", "extrato", "importação"], ["Abra Conciliação bancária e selecione uma conta específica.", "Clique em Selecionar OFX ou CSV.", "Revise válidos, duplicados e inválidos.", "Defina a decisão de cada linha e confirme a importação."]),
        new("confianca-conciliacao", "Níveis de confiança da conciliação", "Entenda como o Nexo encontra movimentações compatíveis.", "Conciliação", "conciliacao", ["confiança", "correspondência", "automática"], ["O valor e a conta precisam ser iguais.", "Datas próximas aumentam a pontuação.", "Descrições semelhantes aumentam a confiança.", "Revise sugestões médias ou baixas antes de conciliar."]),
        new("desfazer-conciliacao", "Como desfazer uma conciliação", "Remova o vínculo mantendo ou estornando a movimentação automática.", "Conciliação", "conciliacao", ["desfazer", "estornar", "histórico"], ["Abra a aba Conciliadas.", "Clique em Desfazer no lançamento desejado.", "Escolha manter a movimentação ou estornar a criada automaticamente.", "Consulte o resultado na aba Histórico."]),
        new("regras-categorizacao", "Criar regras de categorização", "Automatize categoria e centro de custo por descrição.", "Organização", "estrutura", ["regra", "categoria", "centro de custo"], ["Abra Categorias e centros e acesse Regras automáticas.", "Defina o texto que será comparado.", "Escolha categoria, conta e centro de custo.", "Use a prévia antes de aplicar a regra."]),
        new("pagar-receber", "Contas a pagar e receber", "Cadastre parcelas, recorrências e registre baixas.", "Financeiro", "contas", ["pagar", "receber", "parcelas", "baixa"], ["Cadastre o compromisso e informe vencimento e competência.", "Configure parcelas ou recorrência quando necessário.", "Use Registrar baixa para gerar a movimentação financeira.", "Anexe o comprovante ao compromisso."]),
        new("equipe-permissoes", "Papéis e permissões da equipe", "Entenda proprietário, administrador e membro.", "Acesso", "acessos", ["equipe", "permissão", "convite", "usuário"], ["Proprietários controlam o espaço e sua equipe.", "Administradores podem operar e convidar membros.", "Membros possuem acesso de consulta.", "Acessos privilegiados devem utilizar autenticação em dois fatores."]),
        new("saldo-divergente", "O saldo está divergente", "Checklist para localizar diferenças no saldo financeiro.", "Solução de problemas", "conciliacao", ["saldo", "diferença", "divergência"], ["Confirme o saldo inicial da conta.", "Procure movimentações duplicadas.", "Revise lançamentos pendentes na conciliação.", "Verifique transferências e movimentações estornadas."]),
        new("arquivo-recusado", "Arquivo de extrato recusado", "Corrija formato, colunas ou valores inválidos.", "Solução de problemas", "conciliacao", ["arquivo", "erro", "csv", "inválido"], ["Use arquivos OFX ou CSV com até 10 MB.", "No CSV, inclua Data, Descrição e Valor.", "Use valores positivos para entradas e negativos para saídas.", "Confira os motivos apresentados na pré-visualização."]),
        new("seguranca-conta", "Senha e autenticação em dois fatores", "Proteja seu acesso e recupere sua conta.", "Segurança", "configuracoes", ["senha", "2fa", "segurança", "recuperação"], ["Abra Configurações para alterar sua senha.", "Ative a autenticação em dois fatores.", "Guarde os códigos de recuperação em local seguro.", "Se perder o acesso, utilize a recuperação de senha ou abra um chamado."])
    ];

    public IReadOnlyList<HelpArticleView> GetArticles(string? query, string? module)
    {
        var result = Articles.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(module)) result = result.Where(item => item.Module.Equals(module.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            result = result.Where(item => $"{item.Title} {item.Summary} {item.Category} {string.Join(' ', item.Keywords)} {string.Join(' ', item.Steps)}".Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        return result.OrderBy(item => item.Category).ThenBy(item => item.Title).ToList();
    }

    public async Task<IReadOnlyList<SupportTicketView>> GetTicketsAsync(Guid actorId, Guid workspaceId, string scope, CancellationToken ct)
    {
        var actor = await RequireUserAsync(actorId, ct);
        IQueryable<SupportTicket> query = database.SupportTickets.AsNoTracking();
        if (scope.Equals("assigned", StringComparison.OrdinalIgnoreCase))
        {
            RequirePrivilegedMfa(actor);
            var membership = await database.WorkspaceMembers.AsNoTracking().FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == actorId, ct);
            var isManager = membership?.Role is WorkspaceRole.Owner or WorkspaceRole.Administrator;
            var canSupport = actor.GlobalRole == GlobalRole.Support || actor.GlobalRole == GlobalRole.SuperAdmin;
            var canDevelop = actor.GlobalRole == GlobalRole.Developer || actor.GlobalRole == GlobalRole.SuperAdmin;
            query = query.Where(item => isManager && item.WorkspaceId == workspaceId && item.Target == SupportTicketTarget.WorkspaceAdministration
                || canSupport && item.Target == SupportTicketTarget.Support
                || canDevelop && item.Target == SupportTicketTarget.Development);
        }
        else
        {
            await RequireMembershipAsync(actorId, workspaceId, ct);
            query = query.Where(item => item.WorkspaceId == workspaceId && item.RequesterUserId == actorId);
        }
        return await ToViewsAsync(await query.OrderByDescending(item => item.UpdatedAt).Take(200).ToListAsync(ct), ct);
    }

    public async Task<SupportTicketView> CreateTicketAsync(Guid actorId, Guid workspaceId, CreateSupportTicketCommand command, CancellationToken ct)
    {
        await RequireMembershipAsync(actorId, workspaceId, ct);
        var subject = command.Subject.Trim(); var category = command.Category.Trim(); var description = command.Description.Trim();
        if (subject.Length is < 5 or > 140) throw new ArgumentException("O assunto deve ter entre 5 e 140 caracteres.");
        if (category.Length is < 2 or > 60) throw new ArgumentException("Selecione uma categoria válida.");
        if (description.Length is < 10 or > 4000) throw new ArgumentException("A descrição deve ter entre 10 e 4.000 caracteres.");
        var now = DateTimeOffset.UtcNow;
        var ticket = new SupportTicket { Id = Guid.NewGuid(), WorkspaceId = workspaceId, RequesterUserId = actorId, Target = command.Target, Subject = subject, Category = category, Priority = command.Priority, ConsentToShareWorkspaceData = command.ConsentToShareWorkspaceData, CreatedAt = now, UpdatedAt = now };
        database.SupportTickets.Add(ticket);
        database.SupportTicketMessages.Add(new SupportTicketMessage { Id = Guid.NewGuid(), TicketId = ticket.Id, AuthorUserId = actorId, Body = description, CreatedAt = now });
        AddAudit(actorId, workspaceId, "SupportTicketCreated", ticket.Id, $"Chamado aberto: {ticket.Subject}");
        await database.SaveChangesAsync(ct);
        return (await ToViewsAsync([ticket], ct))[0];
    }

    public async Task<SupportTicketView> AddMessageAsync(Guid actorId, Guid workspaceId, Guid ticketId, string body, CancellationToken ct)
    {
        var ticket = await database.SupportTickets.FirstOrDefaultAsync(item => item.Id == ticketId, ct) ?? throw new KeyNotFoundException("Chamado não encontrado.");
        var actor = await RequireUserAsync(actorId, ct);
        var assigned = await CanManageAsync(actor, workspaceId, ticket, ct);
        if (ticket.RequesterUserId == actorId && ticket.WorkspaceId != workspaceId) throw new UnauthorizedAccessException("Selecione o espaço em que este chamado foi aberto.");
        if (ticket.RequesterUserId != actorId && !assigned) throw new UnauthorizedAccessException("Você não pode responder este chamado.");
        var cleanBody = body.Trim(); if (cleanBody.Length is < 2 or > 4000) throw new ArgumentException("A mensagem deve ter entre 2 e 4.000 caracteres.");
        var now = DateTimeOffset.UtcNow;
        database.SupportTicketMessages.Add(new SupportTicketMessage { Id = Guid.NewGuid(), TicketId = ticket.Id, AuthorUserId = actorId, Body = cleanBody, CreatedAt = now });
        ticket.UpdatedAt = now;
        if (assigned && ticket.Status == SupportTicketStatus.Open) ticket.Status = SupportTicketStatus.InProgress;
        if (!assigned && ticket.Status is SupportTicketStatus.WaitingRequester or SupportTicketStatus.Resolved) ticket.Status = SupportTicketStatus.Open;
        AddAudit(actorId, ticket.WorkspaceId, "SupportTicketMessageAdded", ticket.Id, "Nova mensagem adicionada ao chamado");
        await database.SaveChangesAsync(ct);
        return (await ToViewsAsync([ticket], ct))[0];
    }

    public async Task<SupportTicketView> SetStatusAsync(Guid actorId, Guid workspaceId, Guid ticketId, SupportTicketStatus status, CancellationToken ct)
    {
        var ticket = await database.SupportTickets.FirstOrDefaultAsync(item => item.Id == ticketId, ct) ?? throw new KeyNotFoundException("Chamado não encontrado.");
        var actor = await RequireUserAsync(actorId, ct);
        if (!await CanManageAsync(actor, workspaceId, ticket, ct)) throw new UnauthorizedAccessException("Você não pode alterar este chamado.");
        ticket.Status = status; ticket.UpdatedAt = DateTimeOffset.UtcNow; ticket.ClosedAt = status == SupportTicketStatus.Closed ? ticket.UpdatedAt : null;
        AddAudit(actorId, ticket.WorkspaceId, "SupportTicketStatusChanged", ticket.Id, $"Chamado alterado para {status}");
        await database.SaveChangesAsync(ct);
        return (await ToViewsAsync([ticket], ct))[0];
    }

    private async Task<bool> CanManageAsync(ApplicationUser actor, Guid workspaceId, SupportTicket ticket, CancellationToken ct)
    {
        if (ticket.Target == SupportTicketTarget.Support && actor.GlobalRole is GlobalRole.Support or GlobalRole.SuperAdmin) { RequirePrivilegedMfa(actor); return true; }
        if (ticket.Target == SupportTicketTarget.Development && actor.GlobalRole is GlobalRole.Developer or GlobalRole.SuperAdmin) { RequirePrivilegedMfa(actor); return true; }
        if (ticket.Target != SupportTicketTarget.WorkspaceAdministration || ticket.WorkspaceId != workspaceId) return false;
        var membership = await database.WorkspaceMembers.AsNoTracking().FirstOrDefaultAsync(item => item.WorkspaceId == ticket.WorkspaceId && item.UserId == actor.Id, ct);
        return membership?.Role is WorkspaceRole.Owner or WorkspaceRole.Administrator;
    }

    private async Task<IReadOnlyList<SupportTicketView>> ToViewsAsync(IReadOnlyList<SupportTicket> tickets, CancellationToken ct)
    {
        if (tickets.Count == 0) return [];
        var ticketIds = tickets.Select(item => item.Id).ToList();
        var messages = await database.SupportTicketMessages.AsNoTracking().Where(item => ticketIds.Contains(item.TicketId)).OrderBy(item => item.CreatedAt).ToListAsync(ct);
        var userIds = tickets.Select(item => item.RequesterUserId).Concat(messages.Select(item => item.AuthorUserId)).Distinct().ToList();
        var users = await database.Users.AsNoTracking().Where(item => userIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, ct);
        var workspaceIds = tickets.Select(item => item.WorkspaceId).Distinct().ToList();
        var workspaces = await database.Workspaces.AsNoTracking().Where(item => workspaceIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, ct);
        return tickets.Select(ticket => new SupportTicketView(ticket.Id, ticket.WorkspaceId, workspaces.GetValueOrDefault(ticket.WorkspaceId, "Espaço"), ticket.RequesterUserId, users.GetValueOrDefault(ticket.RequesterUserId, "Usuário"), ticket.Target.ToString(), ticket.Subject, ticket.Category, ticket.Priority.ToString(), ticket.Status.ToString(), ticket.ConsentToShareWorkspaceData, ticket.CreatedAt, ticket.UpdatedAt, messages.Where(message => message.TicketId == ticket.Id).Select(message => new SupportTicketMessageView(message.Id, message.AuthorUserId, users.GetValueOrDefault(message.AuthorUserId, "Usuário"), message.Body, message.CreatedAt)).ToList())).ToList();
    }

    private async Task<ApplicationUser> RequireUserAsync(Guid userId, CancellationToken ct)
        => await database.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId && item.IsActive, ct) ?? throw new UnauthorizedAccessException("Usuário sem acesso.");
    private async Task RequireMembershipAsync(Guid userId, Guid workspaceId, CancellationToken ct)
    { if (!await database.WorkspaceMembers.AsNoTracking().AnyAsync(item => item.UserId == userId && item.WorkspaceId == workspaceId, ct)) throw new UnauthorizedAccessException("Você não participa deste espaço."); }
    private static void RequirePrivilegedMfa(ApplicationUser actor)
    { if (actor.GlobalRole != GlobalRole.User && !actor.IsTwoFactorEnabled) throw new UnauthorizedAccessException("Ative a autenticação em dois fatores para acessar chamados atribuídos."); }
    private void AddAudit(Guid userId, Guid workspaceId, string action, Guid entityId, string description)
        => database.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), WorkspaceId = workspaceId, UserId = userId, Action = action, EntityType = "SupportTicket", EntityId = entityId, Description = description, OccurredAt = DateTimeOffset.UtcNow });
}
