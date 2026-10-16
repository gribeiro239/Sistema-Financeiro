using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Financeiro.Domain;
using Financeiro.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Financeiro.Api;

public sealed class EmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool StartTls { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Nexo Financeiro";
    public string PublicUrl { get; set; } = "http://localhost:5173";

    public void Validate(bool development)
    {
        if (!Uri.TryCreate(PublicUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http") || (!development && uri.Scheme != "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("Email:PublicUrl deve ser uma URL pública válida (HTTPS em produção), sem query ou fragmento.");
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(Host) || Port is < 1 or > 65535 || (!development && !StartTls))
            throw new InvalidOperationException("Configure Email:Host, Port e STARTTLS para o envio SMTP.");
        _ = new MailAddress(FromAddress, FromName);
        if (!development && (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password)))
            throw new InvalidOperationException("Configure as credenciais SMTP por variáveis de ambiente.");
    }
}

public sealed record EmailPayload(string Recipient, string Subject, string Body);

// Add the message to the same DbContext/unit of work as the token or invitation.
public sealed class EmailQueue(FinanceDbContext database, IDataProtectionProvider protection, IOptions<EmailOptions> options)
{
    private readonly IDataProtector protector = protection.CreateProtector("NexoFinanceiro.EmailOutbox.v1");

    public void PasswordReset(string recipient, string token, DateTimeOffset expiresAt)
    {
        var url = options.Value.PublicUrl.TrimEnd('/') + "/#reset-password=" + Uri.EscapeDataString(token);
        Enqueue(new(recipient, "Redefina sua senha — Nexo Financeiro",
            $"Recebemos uma solicitação para redefinir sua senha.\n\nAbra: {url}\n\nOu cole este código na tela de recuperação:\n{token}\n\nO código expira em 20 minutos e só pode ser usado uma vez. Se você não solicitou a alteração, ignore este e-mail."), expiresAt);
    }

    public void Invitation(string recipient, string workspaceName, DateTimeOffset expiresAt)
        => Enqueue(new(recipient, "Convite para um espaço — Nexo Financeiro",
            $"Você foi convidado para participar do espaço {workspaceName}.\n\nAcesse {options.Value.PublicUrl.TrimEnd('/')} e entre ou crie sua conta com este e-mail. Em Equipe e acessos, aceite o convite.\n\nO convite expira em sete dias. Nenhum acesso é concedido antes da sua aceitação."), expiresAt);

    private void Enqueue(EmailPayload payload, DateTimeOffset expiresAt)
    {
        var now = DateTimeOffset.UtcNow;
        database.EmailOutboxMessages.Add(new EmailOutboxMessage { Id = Guid.NewGuid(),
            ProtectedPayload = protector.Protect(JsonSerializer.Serialize(payload)), CreatedAt = now,
            ExpiresAt = expiresAt, NextAttemptAt = now });
    }
}

public interface IEmailSender
{
    Task SendAsync(EmailPayload payload, CancellationToken ct);
}

public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailPayload payload, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Enabled) throw new InvalidOperationException("SMTP desativado.");
        using var client = new SmtpClient(settings.Host, settings.Port) { EnableSsl = settings.StartTls,
            UseDefaultCredentials = false, DeliveryMethod = SmtpDeliveryMethod.Network };
        if (!string.IsNullOrEmpty(settings.Username)) client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        using var message = new MailMessage { From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = payload.Subject, Body = payload.Body, BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8 };
        message.To.Add(payload.Recipient);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await client.SendMailAsync(message, timeout.Token);
    }
}

public sealed class EmailOutboxWorker(IServiceScopeFactory scopes, IDataProtectionProvider protection,
    IOptions<EmailOptions> options, ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    private readonly IDataProtector protector = protection.CreateProtector("NexoFinanceiro.EmailOutbox.v1");

    public async Task ProcessOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var now = DateTimeOffset.UtcNow;
        // SQLite does not translate DateTimeOffset comparisons. Bound the batch in memory.
        var pending = await database.EmailOutboxMessages.Where(item => item.SentAt == null).ToListAsync(ct);
        foreach (var item in pending.Where(item => item.ExpiresAt <= now))
        { item.ProtectedPayload = ""; item.LastError = "Expired"; }
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        foreach (var item in pending.Where(item => item.ExpiresAt > now && item.NextAttemptAt <= now && item.Attempts < 6)
                     .OrderBy(item => item.CreatedAt).Take(20))
        {
            if (!options.Value.Enabled) break;
            try
            {
                var payload = JsonSerializer.Deserialize<EmailPayload>(protector.Unprotect(item.ProtectedPayload))
                    ?? throw new InvalidOperationException("Mensagem inválida.");
                await sender.SendAsync(payload, ct);
                item.SentAt = DateTimeOffset.UtcNow; item.LastError = null; item.ProtectedPayload = "";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                item.LastError = "DeliveryFailed";
                logger.LogWarning("Falha no envio da mensagem {MessageId}; nova tentativa agendada.", item.Id);
            }
            item.Attempts++;
            item.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 30 * Math.Pow(2, item.Attempts - 1)));
            await database.SaveChangesAsync(ct);
        }
        await database.SaveChangesAsync(ct);
        // Remove old metadata and expired messages; tokens are never logged or retained after delivery.
        var expired = await database.EmailOutboxMessages.ToListAsync(ct);
        database.EmailOutboxMessages.RemoveRange(expired.Where(item => item.ExpiresAt < now.AddDays(-7)));
        await database.SaveChangesAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Não foi possível processar a fila de e-mails."); }
            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
