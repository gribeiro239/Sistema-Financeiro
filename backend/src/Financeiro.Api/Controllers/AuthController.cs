using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Financeiro.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthenticationService authentication) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthenticatedUser>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var user = await authentication.RegisterAsync(request.Name ?? "", request.Email ?? "", request.Password ?? "", ct);
            await SignInAsync(user, true); return Ok(user);
        }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Cadastro inválido", Detail = exception.Message }); }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthenticatedUser>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await authentication.LoginAsync(request.Email ?? "", request.Password ?? "", ct);
            if (result is null) return Unauthorized(new ProblemDetails { Title = "Não foi possível entrar", Detail = "E-mail, senha ou situação da conta inválidos." });
            if (result.RequiresTwoFactor) return Ok(result);
            await SignInAsync(result.User!, request.RememberMe, false); return Ok(result.User);
        }
        catch (InvalidOperationException exception) { return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails { Title = "Acesso temporariamente bloqueado", Detail = exception.Message }); }
    }

    [AllowAnonymous]
    [HttpPost("login/2fa")]
    public async Task<ActionResult<AuthenticatedUser>> CompleteTwoFactorLogin([FromBody] CompleteTwoFactorLoginRequest request, CancellationToken ct)
    {
        try { var user = await authentication.CompleteTwoFactorLoginAsync(request.ChallengeToken ?? "", request.Code ?? "", ct); await SignInAsync(user, request.RememberMe, true); return Ok(user); }
        catch (ArgumentException exception) { return Unauthorized(new ProblemDetails { Title = "Código inválido", Detail = exception.Message }); }
        catch (InvalidOperationException exception) { return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails { Title = "Acesso temporariamente bloqueado", Detail = exception.Message }); }
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<ActionResult<PasswordRecoveryResult>> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
        => Ok(await authentication.RequestPasswordResetAsync(request.Email ?? "", ct));

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        try
        {
            await authentication.ResetPasswordAsync(request.Token ?? "", request.NewPassword ?? "", ct);
            return NoContent();
        }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível redefinir a senha", Detail = exception.Message }); }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthenticatedUser>> Me(CancellationToken ct)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId)) return Unauthorized();
        var user = await authentication.GetAsync(userId, ct);
        return user is null ? Unauthorized() : Ok(user);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<AuthenticatedUser>> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var user = await authentication.UpdateProfileAsync(userId, request.Name ?? "", request.Email ?? "", ct);
            await SignInAsync(user, true);
            return Ok(user);
        }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Perfil inválido", Detail = exception.Message }); }
        catch (InvalidOperationException) { return NotFound(); }
    }

    [Authorize]
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            await authentication.ChangePasswordAsync(userId, request.CurrentPassword ?? "", request.NewPassword ?? "", ct);
            return NoContent();
        }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível alterar a senha", Detail = exception.Message }); }
        catch (InvalidOperationException) { return NotFound(); }
    }

    [Authorize]
    [HttpPost("2fa/setup")]
    public async Task<ActionResult<TwoFactorSetupResult>> BeginTwoFactorSetup(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { return Ok(await authentication.BeginTwoFactorSetupAsync(userId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [Authorize]
    [HttpPost("2fa/enable")]
    public async Task<ActionResult<TwoFactorEnableResult>> EnableTwoFactor([FromBody] EnableTwoFactorRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { var result = await authentication.EnableTwoFactorAsync(userId, request.Secret ?? "", request.Code ?? "", ct); await SignInAsync(result.User, true, true); return Ok(result); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível ativar o segundo fator", Detail = exception.Message }); }
    }

    [Authorize]
    [HttpDelete("2fa")]
    public async Task<ActionResult<AuthenticatedUser>> DisableTwoFactor([FromBody] DisableTwoFactorRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { var user = await authentication.DisableTwoFactorAsync(userId, request.Password ?? "", request.Code ?? "", ct); await SignInAsync(user, true, false); return Ok(user); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Não foi possível remover o segundo fator", Detail = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new ProblemDetails { Title = "Segundo fator obrigatório", Detail = exception.Message }); }
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private Task SignInAsync(AuthenticatedUser user, bool persistent, bool mfa = false)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Name), new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.GlobalRole), new Claim("security_stamp", user.SecurityStamp), new Claim("mfa", mfa ? "true" : "false") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = persistent, AllowRefresh = true, ExpiresUtc = persistent ? DateTimeOffset.UtcNow.AddDays(14) : null });
    }
}

public sealed record RegisterRequest(string? Name, string? Email, string? Password);
public sealed record LoginRequest(string? Email, string? Password, bool RememberMe = false);
public sealed record UpdateProfileRequest(string? Name, string? Email);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public sealed record ForgotPasswordRequest(string? Email);
public sealed record ResetPasswordRequest(string? Token, string? NewPassword);
public sealed record CompleteTwoFactorLoginRequest(string? ChallengeToken, string? Code, bool RememberMe = false);
public sealed record EnableTwoFactorRequest(string? Secret, string? Code);
public sealed record DisableTwoFactorRequest(string? Password, string? Code);
