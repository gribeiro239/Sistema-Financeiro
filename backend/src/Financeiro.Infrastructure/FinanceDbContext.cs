using Financeiro.Domain;
using Microsoft.EntityFrameworkCore;

namespace Financeiro.Infrastructure;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<TwoFactorRecoveryCode> TwoFactorRecoveryCodes => Set<TwoFactorRecoveryCode>();
    public DbSet<TwoFactorLoginChallenge> TwoFactorLoginChallenges => Set<TwoFactorLoginChallenge>();
    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();
    public DbSet<PlatformAuditEntry> PlatformAuditEntries => Set<PlatformAuditEntry>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<FinancialAccount> Accounts => Set<FinancialAccount>();
    public DbSet<FinancialTransaction> Transactions => Set<FinancialTransaction>();
    public DbSet<FinancialCommitment> Commitments => Set<FinancialCommitment>();
    public DbSet<BankStatementEntry> BankStatementEntries => Set<BankStatementEntry>();
    public DbSet<ReconciliationHistoryEntry> ReconciliationHistoryEntries => Set<ReconciliationHistoryEntry>();
    public DbSet<FinancialCategory> Categories => Set<FinancialCategory>();
    public DbSet<CategorizationRule> CategorizationRules => Set<CategorizationRule>();
    public DbSet<DocumentAttachment> DocumentAttachments => Set<DocumentAttachment>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<TransactionAllocation> TransactionAllocations => Set<TransactionAllocation>();
    public DbSet<MonthlyBudget> MonthlyBudgets => Set<MonthlyBudget>();
    public DbSet<FinancialGoal> FinancialGoals => Set<FinancialGoal>();
    public DbSet<GoalContribution> GoalContributions => Set<GoalContribution>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailOutboxMessage>(entity =>
        {
            entity.ToTable("EmailOutboxMessages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ProtectedPayload).IsRequired();
            entity.Property(item => item.LastError).HasMaxLength(100);
            entity.HasIndex(item => new { item.SentAt, item.NextAttemptAt });
        });
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.ToTable("Workspaces");
            entity.HasKey(workspace => workspace.Id);
            entity.Property(workspace => workspace.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(workspace => workspace.Name);
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Name).HasMaxLength(80).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(254).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(user => user.SecurityStamp).HasMaxLength(64).IsRequired();
            entity.Property(user => user.TwoFactorSecret).HasMaxLength(128);
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<PlatformAuditEntry>(entity =>
        {
            entity.ToTable("PlatformAuditEntries");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Action).HasMaxLength(40).IsRequired();
            entity.Property(item => item.EntityType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(240).IsRequired();
            entity.Property(item => item.Details).HasMaxLength(4000);
            entity.Property(item => item.IpAddress).HasMaxLength(64);
            entity.HasIndex(item => item.OccurredAt);
            entity.HasIndex(item => item.ActorUserId);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlatformSetting>(entity =>
        {
            entity.ToTable("PlatformSettings");
            entity.HasKey(item => item.Key);
            entity.Property(item => item.Key).HasMaxLength(80);
            entity.Property(item => item.Value).HasMaxLength(500).IsRequired();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkspaceMember>(entity =>
        {
            entity.ToTable("WorkspaceMembers");
            entity.HasKey(member => new { member.WorkspaceId, member.UserId });
            entity.HasIndex(member => member.UserId);
            entity.HasOne<Workspace>().WithMany().HasForeignKey(member => member.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("PasswordResetTokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => new { token.UserId, token.ExpiresAt });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TwoFactorRecoveryCode>(entity =>
        {
            entity.ToTable("TwoFactorRecoveryCodes"); entity.HasKey(item => item.Id);
            entity.Property(item => item.CodeHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.UsedAt });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TwoFactorLoginChallenge>(entity =>
        {
            entity.ToTable("TwoFactorLoginChallenges"); entity.HasKey(item => item.Id);
            entity.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => new { item.UserId, item.ExpiresAt });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkspaceInvitation>(entity =>
        {
            entity.ToTable("WorkspaceInvitations"); entity.HasKey(item => item.Id);
            entity.Property(item => item.Email).HasMaxLength(254).IsRequired();
            entity.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => new { item.WorkspaceId, item.Email, item.Status });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(item => item.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.ToTable("Contacts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.TaxId).HasMaxLength(20);
            entity.Property(item => item.Email).HasMaxLength(254);
            entity.Property(item => item.Phone).HasMaxLength(30);
            entity.HasIndex(item => new { item.WorkspaceId, item.Name });
            entity.HasIndex(item => new { item.WorkspaceId, item.IsActive, item.Kind });
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable("AuditEntries");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Action).HasMaxLength(40).IsRequired();
            entity.Property(item => item.EntityType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(240).IsRequired();
            entity.Property(item => item.Details).HasMaxLength(4000);
            entity.HasIndex(item => new { item.WorkspaceId, item.OccurredAt });
            entity.HasIndex(item => new { item.EntityType, item.EntityId });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FinancialAccount>(entity =>
        {
            entity.ToTable("Accounts");
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Name).HasMaxLength(80).IsRequired();
            entity.Property(account => account.Institution).HasMaxLength(80).IsRequired();
            entity.Property(account => account.LastFourDigits).HasMaxLength(4);
            entity.Property(account => account.OpeningBalance).HasPrecision(18, 2);
            entity.HasIndex(account => new { account.WorkspaceId, account.IsActive });
        });

        modelBuilder.Entity<FinancialTransaction>(entity =>
        {
            entity.ToTable("Transactions");
            entity.HasKey(transaction => transaction.Id);
            entity.Property(transaction => transaction.Description).HasMaxLength(120).IsRequired();
            entity.Property(transaction => transaction.Category).HasMaxLength(60).IsRequired();
            entity.Property(transaction => transaction.Amount).HasPrecision(18, 2);
            entity.Ignore(transaction => transaction.SignedAmount);
            entity.HasIndex(transaction => new { transaction.WorkspaceId, transaction.OccurredOn });
            entity.HasIndex(transaction => transaction.AccountId);
            entity.HasIndex(transaction => transaction.TransferId);
            entity.HasOne<FinancialAccount>().WithMany().HasForeignKey(transaction => transaction.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancialAccount>().WithMany().HasForeignKey(transaction => transaction.CounterpartyAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancialCommitment>().WithMany().HasForeignKey(transaction => transaction.CommitmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FinancialCommitment>(entity =>
        {
            entity.ToTable("Commitments");
            entity.HasKey(commitment => commitment.Id);
            entity.Property(commitment => commitment.Description).HasMaxLength(120).IsRequired();
            entity.Property(commitment => commitment.Contact).HasMaxLength(100).IsRequired();
            entity.Property(commitment => commitment.Category).HasMaxLength(60).IsRequired();
            entity.Property(commitment => commitment.TotalAmount).HasPrecision(18, 2);
            entity.Property(commitment => commitment.SettledAmount).HasPrecision(18, 2);
            entity.Ignore(commitment => commitment.RemainingAmount);
            entity.HasIndex(commitment => new { commitment.WorkspaceId, commitment.DueDate });
            entity.HasIndex(commitment => new { commitment.WorkspaceId, commitment.Status, commitment.Kind });
            entity.HasIndex(commitment => commitment.SeriesId);
            entity.HasOne<Contact>().WithMany().HasForeignKey(commitment => commitment.ContactId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BankStatementEntry>(entity =>
        {
            entity.ToTable("BankStatementEntries");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ImportKey).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ExternalId).HasMaxLength(120);
            entity.Property(item => item.Description).HasMaxLength(180).IsRequired();
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Ignore(item => item.SignedAmount);
            entity.HasIndex(item => new { item.WorkspaceId, item.AccountId, item.ImportKey }).IsUnique();
            entity.HasIndex(item => new { item.WorkspaceId, item.Status, item.OccurredOn });
            entity.HasOne<FinancialAccount>().WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancialTransaction>().WithMany().HasForeignKey(item => item.TransactionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReconciliationHistoryEntry>(entity =>
        {
            entity.ToTable("ReconciliationHistoryEntries");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Action).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(180).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(80);
            entity.Property(item => item.CostCenterName).HasMaxLength(100);
            entity.Property(item => item.RuleName).HasMaxLength(80);
            entity.Property(item => item.Details).HasMaxLength(1000);
            entity.HasIndex(item => new { item.WorkspaceId, item.OccurredAt });
            entity.HasIndex(item => new { item.BankStatementEntryId, item.OccurredAt });
            entity.HasOne<BankStatementEntry>().WithMany().HasForeignKey(item => item.BankStatementEntryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<FinancialAccount>().WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FinancialCategory>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.Name }).IsUnique();
            entity.HasIndex(item => item.ParentId);
            entity.HasOne<FinancialCategory>().WithMany().HasForeignKey(item => item.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CategorizationRule>(entity =>
        {
            entity.ToTable("CategorizationRules");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(80).IsRequired();
            entity.Property(item => item.DescriptionPattern).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(80).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.Priority });
            entity.HasIndex(item => new { item.WorkspaceId, item.IsActive });
            entity.HasOne<FinancialAccount>().WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CostCenter>().WithMany().HasForeignKey(item => item.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentAttachment>(entity =>
        {
            entity.ToTable("DocumentAttachments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EntityType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.OriginalFileName).HasMaxLength(180).IsRequired();
            entity.Property(item => item.StoredFileName).HasMaxLength(80).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.EntityType, item.EntityId });
            entity.HasIndex(item => item.StoredFileName).IsUnique();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CostCenter>(entity =>
        {
            entity.ToTable("CostCenters");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(20).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.Code }).IsUnique();
        });

        modelBuilder.Entity<TransactionAllocation>(entity =>
        {
            entity.ToTable("TransactionAllocations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.TransactionId, item.CostCenterId }).IsUnique();
            entity.HasIndex(item => item.CostCenterId);
            entity.HasOne<FinancialTransaction>().WithMany().HasForeignKey(item => item.TransactionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CostCenter>().WithMany().HasForeignKey(item => item.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MonthlyBudget>(entity =>
        {
            entity.ToTable("MonthlyBudgets");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PlannedAmount).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.WorkspaceId, item.Year, item.Month, item.TargetKind, item.TargetId }).IsUnique();
            entity.HasIndex(item => new { item.WorkspaceId, item.Year, item.Month });
        });

        modelBuilder.Entity<FinancialGoal>(entity =>
        {
            entity.ToTable("FinancialGoals");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(80);
            entity.Property(item => item.TargetAmount).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.WorkspaceId, item.IsActive });
            entity.HasIndex(item => new { item.WorkspaceId, item.DueDate });
        });

        modelBuilder.Entity<GoalContribution>(entity =>
        {
            entity.ToTable("GoalContributions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Note).HasMaxLength(160);
            entity.HasIndex(item => new { item.GoalId, item.ContributedOn });
            entity.HasOne<FinancialGoal>().WithMany().HasForeignKey(item => item.GoalId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.ToTable("SupportTickets");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Subject).HasMaxLength(140).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(60).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.Status, item.UpdatedAt });
            entity.HasIndex(item => new { item.Target, item.Status, item.UpdatedAt });
            entity.HasIndex(item => item.RequesterUserId);
            entity.HasOne<Workspace>().WithMany().HasForeignKey(item => item.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.RequesterUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupportTicketMessage>(entity =>
        {
            entity.ToTable("SupportTicketMessages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Body).HasMaxLength(4000).IsRequired();
            entity.HasIndex(item => new { item.TicketId, item.CreatedAt });
            entity.HasOne<SupportTicket>().WithMany().HasForeignKey(item => item.TicketId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
