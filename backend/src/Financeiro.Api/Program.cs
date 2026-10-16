using System.Text.Json.Serialization;
using System.Security.Claims;
using Financeiro.Api;
using Financeiro.Application;
using Financeiro.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// Backup switches belong to our offline tool, not the host configuration parser.
var hostArguments = new List<string>();
for (var index = 0; index < args.Length; index++)
{
    if (args[index] == "--backup-create") continue;
    if (args[index] is "--backup-verify" or "--backup-restore" or "--target")
    {
        if (index + 1 < args.Length && !args[index + 1].StartsWith("--")) index++;
        continue;
    }
    hostArguments.Add(args[index]);
}
var builder = WebApplication.CreateBuilder(hostArguments.ToArray());
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IWorkspaceContext, WorkspaceContext>();
builder.Services.AddScoped<IUserContext, UserContext>();
builder.Services.AddScoped<Financeiro.Api.AuthenticationService>();
builder.Services.AddScoped<PlatformAdministrationService>();
builder.Services.AddScoped<WorkspaceAccessService>();
builder.Services.AddScoped<HelpCenterService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection("Backups"));
builder.Services.AddScoped<EmailQueue>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<EmailOutboxWorker>();
builder.Services.AddSingleton<BackupFileGate>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddHostedService<AutomaticBackupWorker>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "nexo.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    options.Events.OnValidatePrincipal = async context =>
    {
        var value = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = context.Principal?.FindFirstValue("security_stamp");
        if (!Guid.TryParse(value, out var userId)) { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(); return; }
        var database = context.HttpContext.RequestServices.GetRequiredService<FinanceDbContext>();
        var user = await database.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId, context.HttpContext.RequestAborted);
        if (user is null || !user.IsActive || !string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal)) { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(); }
    };
});
builder.Services.AddAuthorization(options => options.AddPolicy("PrivilegedMfa", policy => policy.RequireClaim("mfa", "true")));
var dataProtectionDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionDirectory);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(dataProtectionDirectory)).SetApplicationName("NexoFinanceiro");
var configuredDatabaseFile = builder.Configuration["Database:File"] ?? Path.Combine("App_Data", "finance.db");
var databaseFile = Path.IsPathRooted(configuredDatabaseFile)
    ? configuredDatabaseFile
    : Path.Combine(builder.Environment.ContentRootPath, configuredDatabaseFile);
Directory.CreateDirectory(Path.GetDirectoryName(databaseFile)!);
builder.Services.AddFinanceInfrastructure($"Data Source={databaseFile};Foreign Keys=True");
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
// Offline tools do not initialize/migrate/seed the live database or start background workers.
if (args.Any(argument => argument.StartsWith("--backup-", StringComparison.Ordinal)))
{
    try
    {
        var backups = app.Services.GetRequiredService<BackupService>();
        string ArgumentAfter(string name)
        {
            var index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length || args[index + 1].StartsWith("--"))
                throw new ArgumentException($"Informe o valor de {name}.");
            return args[index + 1];
        }
        object result;
        if (args.Contains("--backup-create")) result = await backups.CreateAsync(CancellationToken.None);
        else if (args.Contains("--backup-verify")) result = await backups.VerifyAsync(ArgumentAfter("--backup-verify"), CancellationToken.None);
        else if (args.Contains("--backup-restore")) result = await backups.RestoreAsync(ArgumentAfter("--backup-restore"), ArgumentAfter("--target"), CancellationToken.None);
        else throw new ArgumentException("Comando de backup desconhecido.");
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Operação de backup falhou: {exception.GetType().Name}. Verifique arquivo, chave, destino e configuração.");
        Environment.ExitCode = 1;
    }
    await app.DisposeAsync();
    return;
}
app.Services.GetRequiredService<IOptions<EmailOptions>>().Value.Validate(app.Environment.IsDevelopment());
await app.Services.InitializeFinanceDatabaseAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var isScopedApi = context.Request.Path.StartsWithSegments("/api")
        && !context.Request.Path.StartsWithSegments("/api/workspaces")
        && !context.Request.Path.StartsWithSegments("/api/workspace-invitations")
        && !context.Request.Path.StartsWithSegments("/api/auth")
        && !context.Request.Path.StartsWithSegments("/api/platform")
        && !context.Request.Path.StartsWithSegments("/api/health");
    if (isScopedApi && context.User.Identity?.IsAuthenticated == true)
    {
        var supplied = context.Request.Headers[WorkspaceContext.HeaderName].FirstOrDefault();
        var workspaceId = FinanceService.SeedWorkspaceId;
        if (supplied is not null && !Guid.TryParse(supplied, out workspaceId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ProblemDetails { Title = "Espaço inválido", Detail = "O identificador do espaço não é válido." });
            return;
        }
        await using var scope = context.RequestServices.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var userValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var membership = Guid.TryParse(userValue, out var userId) ? await database.WorkspaceMembers.AsNoTracking().FirstOrDefaultAsync(member => member.WorkspaceId == workspaceId && member.UserId == userId, context.RequestAborted) : null;
        if (membership is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails { Title = "Acesso negado", Detail = "Você não participa deste espaço." });
            return;
        }
        var safeMethod = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method);
        var isHelpRequest = context.Request.Path.StartsWithSegments("/api/help");
        if (!safeMethod && membership.Role == Financeiro.Domain.WorkspaceRole.Member && !isHelpRequest)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails { Title = "Acesso somente leitura", Detail = "Membros podem consultar os dados, mas apenas administradores e proprietários podem alterá-los." });
            return;
        }
    }
    await next();
});
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var mutatesAttachments = context.Request.Path.StartsWithSegments("/api/attachments") &&
        !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method);
    if (!mutatesAttachments) { await next(); return; }
    var gate = context.RequestServices.GetRequiredService<BackupFileGate>();
    await gate.Semaphore.WaitAsync(context.RequestAborted);
    try { await next(); }
    finally { gate.Semaphore.Release(); }
});
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();
