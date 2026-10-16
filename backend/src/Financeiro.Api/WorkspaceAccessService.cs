using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Financeiro.Domain;
using Financeiro.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Api;

public sealed record WorkspaceMemberView(Guid UserId, string Name, string Email, string Role, DateTimeOffset JoinedAt);
public sealed record WorkspaceInvitationView(Guid Id, Guid WorkspaceId, string WorkspaceName, string Email, string Role, string Status, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt);

public sealed class WorkspaceAccessService(FinanceDbContext database, EmailQueue emailQueue)
{
    public async Task<IReadOnlyList<WorkspaceMemberView>> GetMembersAsync(Guid actorId, Guid workspaceId, CancellationToken ct)
    {
        await RequireManagerAsync(actorId, workspaceId, ct);
        var members = await database.WorkspaceMembers.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).ToListAsync(ct);
        var users = await database.Users.AsNoTracking().Where(item => members.Select(member => member.UserId).Contains(item.Id)).ToDictionaryAsync(item => item.Id, ct);
        return members.OrderBy(item => item.Role).ThenBy(item => users[item.UserId].Name).Select(item => new WorkspaceMemberView(item.UserId, users[item.UserId].Name, users[item.UserId].Email, item.Role.ToString(), item.JoinedAt)).ToList();
    }

    public async Task<WorkspaceInvitationView> InviteAsync(Guid actorId, Guid workspaceId, string email, WorkspaceRole role, CancellationToken ct)
    {
        var actor = await RequireManagerAsync(actorId, workspaceId, ct);
        if (role == WorkspaceRole.Owner) throw new ArgumentException("Use a transferência de propriedade para definir um proprietário.");
        if (actor.Role == WorkspaceRole.Administrator && role != WorkspaceRole.Member) throw new UnauthorizedAccessException("Administradores podem convidar apenas membros.");
        var cleanEmail = NormalizeEmail(email);
        var target = await database.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Email == cleanEmail, ct);
        if (target is not null && await database.WorkspaceMembers.AnyAsync(item => item.WorkspaceId == workspaceId && item.UserId == target.Id, ct)) throw new ArgumentException("Este usuário já participa do espaço.");
        var pending = await database.WorkspaceInvitations.FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.Email == cleanEmail && item.Status == WorkspaceInvitationStatus.Pending, ct);
        if (pending is not null && pending.ExpiresAt > DateTimeOffset.UtcNow) throw new ArgumentException("Já existe um convite pendente para este e-mail.");
        if (pending is not null) pending.Status = WorkspaceInvitationStatus.Expired;
        var invitation = new WorkspaceInvitation { Id = Guid.NewGuid(), WorkspaceId = workspaceId, InvitedByUserId = actorId, Email = cleanEmail, Role = role, TokenHash = HashToken(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))), Status = WorkspaceInvitationStatus.Pending, ExpiresAt = DateTimeOffset.UtcNow.AddDays(7), CreatedAt = DateTimeOffset.UtcNow };
        var workspaceName = await database.Workspaces.Where(item => item.Id == workspaceId).Select(item => item.Name).FirstAsync(ct);
        emailQueue.Invitation(cleanEmail, workspaceName, invitation.ExpiresAt);
        database.WorkspaceInvitations.Add(invitation); AddAudit(actorId, workspaceId, "MemberInvited", invitation.Id, $"Convite criado para {cleanEmail}", new { Role = role }); await database.SaveChangesAsync(ct);
        return await ToViewAsync(invitation, ct);
    }

    public async Task<IReadOnlyList<WorkspaceInvitationView>> GetWorkspaceInvitationsAsync(Guid actorId, Guid workspaceId, CancellationToken ct)
    {
        await RequireManagerAsync(actorId, workspaceId, ct); var now = DateTimeOffset.UtcNow;
        var invitations = await database.WorkspaceInvitations.Where(item => item.WorkspaceId == workspaceId).OrderByDescending(item => item.CreatedAt).Take(100).ToListAsync(ct);
        foreach (var item in invitations.Where(item => item.Status == WorkspaceInvitationStatus.Pending && item.ExpiresAt <= now)) item.Status = WorkspaceInvitationStatus.Expired;
        if (database.ChangeTracker.HasChanges()) await database.SaveChangesAsync(ct);
        var name = await database.Workspaces.Where(item => item.Id == workspaceId).Select(item => item.Name).FirstAsync(ct);
        return invitations.Select(item => new WorkspaceInvitationView(item.Id, item.WorkspaceId, name, item.Email, item.Role.ToString(), item.Status.ToString(), item.ExpiresAt, item.CreatedAt)).ToList();
    }

    public async Task<IReadOnlyList<WorkspaceInvitationView>> GetInvitationsAsync(Guid userId, CancellationToken ct)
    {
        var user = await database.Users.AsNoTracking().FirstAsync(item => item.Id == userId, ct); var now = DateTimeOffset.UtcNow;
        var invitations = await database.WorkspaceInvitations.Where(item => item.Email == user.Email && item.Status == WorkspaceInvitationStatus.Pending).ToListAsync(ct);
        foreach (var item in invitations.Where(item => item.ExpiresAt <= now)) item.Status = WorkspaceInvitationStatus.Expired;
        if (database.ChangeTracker.HasChanges()) await database.SaveChangesAsync(ct);
        var active = invitations.Where(item => item.Status == WorkspaceInvitationStatus.Pending).OrderByDescending(item => item.CreatedAt).ToList();
        var workspaceNames = await database.Workspaces.AsNoTracking().Where(item => active.Select(invite => invite.WorkspaceId).Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, ct);
        return active.Select(item => new WorkspaceInvitationView(item.Id, item.WorkspaceId, workspaceNames.GetValueOrDefault(item.WorkspaceId, "Espaço"), item.Email, item.Role.ToString(), item.Status.ToString(), item.ExpiresAt, item.CreatedAt)).ToList();
    }

    public async Task AcceptInvitationAsync(Guid userId, Guid invitationId, CancellationToken ct)
    {
        var user = await database.Users.AsNoTracking().FirstAsync(item => item.Id == userId, ct);
        var invitation = await database.WorkspaceInvitations.FirstOrDefaultAsync(item => item.Id == invitationId && item.Email == user.Email, ct) ?? throw new KeyNotFoundException("Convite não encontrado.");
        if (invitation.Status != WorkspaceInvitationStatus.Pending || invitation.ExpiresAt <= DateTimeOffset.UtcNow) throw new InvalidOperationException("Este convite não está mais disponível.");
        if (!await database.WorkspaceMembers.AnyAsync(item => item.WorkspaceId == invitation.WorkspaceId && item.UserId == userId, ct)) database.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = invitation.WorkspaceId, UserId = userId, Role = invitation.Role, JoinedAt = DateTimeOffset.UtcNow });
        invitation.Status = WorkspaceInvitationStatus.Accepted; invitation.AcceptedAt = DateTimeOffset.UtcNow; AddAudit(userId, invitation.WorkspaceId, "MemberJoined", userId, $"Membro ingressou no espaço: {user.Email}", new { invitation.Role }); await database.SaveChangesAsync(ct);
    }

    public async Task RevokeInvitationAsync(Guid actorId, Guid workspaceId, Guid invitationId, CancellationToken ct)
    {
        await RequireManagerAsync(actorId, workspaceId, ct); var invitation = await database.WorkspaceInvitations.FirstOrDefaultAsync(item => item.Id == invitationId && item.WorkspaceId == workspaceId, ct) ?? throw new KeyNotFoundException("Convite não encontrado.");
        if (invitation.Status != WorkspaceInvitationStatus.Pending) throw new InvalidOperationException("O convite não está pendente."); invitation.Status = WorkspaceInvitationStatus.Revoked; AddAudit(actorId, workspaceId, "InvitationRevoked", invitation.Id, $"Convite revogado: {invitation.Email}", null); await database.SaveChangesAsync(ct);
    }

    public async Task<WorkspaceMemberView> ChangeRoleAsync(Guid actorId, Guid workspaceId, Guid memberId, WorkspaceRole role, CancellationToken ct)
    {
        var actor = await RequireOwnerAsync(actorId, workspaceId, ct); if (role == WorkspaceRole.Owner) throw new ArgumentException("Use a transferência de propriedade."); if (actorId == memberId) throw new InvalidOperationException("O proprietário não pode alterar o próprio papel.");
        var member = await database.WorkspaceMembers.FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == memberId, ct) ?? throw new KeyNotFoundException("Membro não encontrado."); var previous = member.Role; member.Role = role; AddAudit(actorId, workspaceId, "MemberRoleChanged", memberId, "Papel de membro alterado.", new { PreviousRole = previous, NewRole = role }); await database.SaveChangesAsync(ct); return (await GetMembersAsync(actorId, workspaceId, ct)).First(item => item.UserId == memberId);
    }

    public async Task RemoveMemberAsync(Guid actorId, Guid workspaceId, Guid memberId, CancellationToken ct)
    {
        var actor = await RequireManagerAsync(actorId, workspaceId, ct); var member = await database.WorkspaceMembers.FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == memberId, ct) ?? throw new KeyNotFoundException("Membro não encontrado.");
        if (member.Role == WorkspaceRole.Owner) throw new InvalidOperationException("Transfira a propriedade antes de remover o proprietário."); if (actor.Role == WorkspaceRole.Administrator && member.Role != WorkspaceRole.Member) throw new UnauthorizedAccessException("Administradores podem remover apenas membros.");
        database.WorkspaceMembers.Remove(member); AddAudit(actorId, workspaceId, "MemberRemoved", memberId, "Membro removido do espaço.", new { member.Role }); await database.SaveChangesAsync(ct);
    }

    public async Task TransferOwnershipAsync(Guid actorId, Guid workspaceId, Guid newOwnerId, CancellationToken ct)
    {
        var owner = await RequireOwnerAsync(actorId, workspaceId, ct); if (actorId == newOwnerId) throw new ArgumentException("Este usuário já é o proprietário.");
        var next = await database.WorkspaceMembers.FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == newOwnerId, ct) ?? throw new KeyNotFoundException("O novo proprietário precisa ser membro do espaço."); owner.Role = WorkspaceRole.Administrator; next.Role = WorkspaceRole.Owner; AddAudit(actorId, workspaceId, "OwnershipTransferred", newOwnerId, "Propriedade do espaço transferida.", new { PreviousOwnerId = actorId, NewOwnerId = newOwnerId }); await database.SaveChangesAsync(ct);
    }

    private async Task<WorkspaceMember> RequireManagerAsync(Guid userId, Guid workspaceId, CancellationToken ct)
    { var membership = await database.WorkspaceMembers.FirstOrDefaultAsync(item => item.UserId == userId && item.WorkspaceId == workspaceId, ct) ?? throw new KeyNotFoundException("Espaço não encontrado."); if (membership.Role == WorkspaceRole.Member) throw new UnauthorizedAccessException("Seu papel não permite gerenciar membros."); return membership; }
    private async Task<WorkspaceMember> RequireOwnerAsync(Guid userId, Guid workspaceId, CancellationToken ct)
    { var membership = await RequireManagerAsync(userId, workspaceId, ct); if (membership.Role != WorkspaceRole.Owner) throw new UnauthorizedAccessException("Somente o proprietário pode realizar esta ação."); return membership; }
    private async Task<WorkspaceInvitationView> ToViewAsync(WorkspaceInvitation item, CancellationToken ct) { var name = await database.Workspaces.Where(workspace => workspace.Id == item.WorkspaceId).Select(workspace => workspace.Name).FirstAsync(ct); return new(item.Id, item.WorkspaceId, name, item.Email, item.Role.ToString(), item.Status.ToString(), item.ExpiresAt, item.CreatedAt); }
    private void AddAudit(Guid actorId, Guid workspaceId, string action, Guid entityId, string description, object? details) => database.AuditEntries.Add(new AuditEntry { Id = Guid.NewGuid(), WorkspaceId = workspaceId, UserId = actorId, Action = action, EntityType = "WorkspaceAccess", EntityId = entityId, Description = description, Details = details is null ? null : JsonSerializer.Serialize(details), OccurredAt = DateTimeOffset.UtcNow });
    private static string NormalizeEmail(string email) { var clean = email.Trim().ToLowerInvariant(); try { _ = new MailAddress(clean); } catch { throw new ArgumentException("Informe um e-mail válido."); } return clean; }
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
