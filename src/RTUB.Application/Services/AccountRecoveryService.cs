using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>Forgot-password: the address the reset link is sent to.</summary>
public sealed record ForgotPasswordInput(string? Email);

/// <summary>Reset-password: the link's <c>email</c> and <c>code</c> (as the browser decoded them once), and the new password twice.</summary>
public sealed record ResetPasswordInput(string? Email, string? Code, string? Password, string? ConfirmPassword);

/// <summary>Confirm-email: the link's <c>userId</c> and <c>code</c> (as the browser decoded them once).</summary>
public sealed record ConfirmEmailInput(string? UserId, string? Code);

/// <summary>What the page shows: whether it worked, and the sentence to show.</summary>
public sealed record AccountRecoveryResult(bool Succeeded, string Message);

/// <summary>
/// The public account-recovery flows of the React /forgot-password, /reset-password and /confirm-email (task 036,
/// were Blazor pages). The same Identity calls as before, with the same token handling and the same messages; nothing
/// says whether an address has an account, nothing signs anyone in, and no token is logged or returned.
/// </summary>
public class AccountRecoveryService
{
    public const string ForgotMessage = "Se existir uma conta para este email, foi enviado um link de recuperação de palavra-passe.";
    public const int PasswordMin = 8;
    public const int PasswordMax = 100;

    private static readonly EmailAddressAttribute EmailFormat = new();

    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailSender _email;
    private readonly IEmailTemplateRenderer _templates;
    private readonly ILogger<AccountRecoveryService> _logger;

    public AccountRecoveryService(UserManager<ApplicationUser> users, IEmailSender email, IEmailTemplateRenderer templates,
        ILogger<AccountRecoveryService> logger)
    {
        _users = users;
        _email = email;
        _templates = templates;
        _logger = logger;
    }

    /// <summary>
    /// Sends the reset link to a confirmed account; answers the same neutral sentence whether or not there is one.
    /// The link is {baseUrl}/reset-password?code={url-encoded token}&amp;email={url-encoded email}, as before.
    /// </summary>
    public async Task<EventResult<AccountRecoveryResult>> ForgotAsync(ForgotPasswordInput input, string baseUrl)
    {
        var email = input.Email?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            return EventResult<AccountRecoveryResult>.Invalid("email", "O email é obrigatório");
        }

        if (!EmailFormat.IsValid(email))
        {
            return EventResult<AccountRecoveryResult>.Invalid("email", "Por favor, introduza um email válido");
        }

        var user = await _users.FindByEmailAsync(email);
        if (user is not null && await _users.IsEmailConfirmedAsync(user))
        {
            try
            {
                var token = await _users.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = $"{baseUrl.TrimEnd('/')}/reset-password?code={WebUtility.UrlEncode(token)}&email={WebUtility.UrlEncode(email)}";
                var html = await _templates.RenderPasswordResetAsync(callbackUrl);
                await _email.SendEmailAsync(email, "Recuperação de palavra-passe", html);
            }
            catch (Exception ex)
            {
                // Answered like every other address, so a failed send does not tell an account apart. No token or link here.
                _logger.LogError(ex, "Password reset email could not be sent for user {UserName}", user.UserName);
            }
        }

        return EventResult<AccountRecoveryResult>.Ok(new AccountRecoveryResult(true, ForgotMessage));
    }

    /// <summary>
    /// Resets the password with the link's token, as before: the code is used as the browser decoded it (never decoded
    /// again), an unknown address answers the same neutral success, and Identity's own errors are shown as they come
    /// (except an invalid or expired token, said in Portuguese).
    /// </summary>
    public async Task<EventResult<AccountRecoveryResult>> ResetAsync(ResetPasswordInput input)
    {
        if (string.IsNullOrEmpty(input.Email) || string.IsNullOrEmpty(input.Code))
        {
            return EventResult<AccountRecoveryResult>.Invalid("code", "Link de redefinição de palavra-passe inválido.");
        }

        var errors = new Dictionary<string, string[]>();
        if (!EmailFormat.IsValid(input.Email))
        {
            errors["email"] = ["Por favor, introduza um email válido"];
        }

        if (string.IsNullOrEmpty(input.Password))
        {
            errors["password"] = ["A palavra-passe é obrigatória"];
        }
        else if (input.Password.Length is < PasswordMin or > PasswordMax)
        {
            errors["password"] = [$"A palavra-passe deve ter pelo menos {PasswordMin} caracteres."];
        }

        if (string.IsNullOrEmpty(input.ConfirmPassword))
        {
            errors["confirmPassword"] = ["A confirmação de palavra-passe é obrigatória"];
        }
        else if (input.ConfirmPassword != input.Password)
        {
            errors["confirmPassword"] = ["A palavra-passe e a confirmação não coincidem."];
        }

        if (errors.Count > 0)
        {
            return new EventResult<AccountRecoveryResult>(EventResultStatus.Invalid, Errors: errors);
        }

        var user = await _users.FindByEmailAsync(input.Email);
        if (user is null)
        {
            // Do not reveal that the user does not exist.
            return EventResult<AccountRecoveryResult>.Ok(new AccountRecoveryResult(true, "Redefinição de palavra-passe concluída."));
        }

        var result = await _users.ResetPasswordAsync(user, input.Code, input.Password!);
        return result.Succeeded
            ? EventResult<AccountRecoveryResult>.Ok(new AccountRecoveryResult(true,
                "A sua palavra-passe foi redefinida com sucesso. Pode agora fazer login com a nova palavra-passe."))
            : EventResult<AccountRecoveryResult>.Invalid("reset", result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? "Este link de redefinição é inválido ou já expirou. Pede um novo link."
                : string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    /// <summary>
    /// Confirms the address with the link's token, as before: the code is URL-decoded once more on top of the browser's
    /// decoding (the old page did the same), and nobody is signed in.
    /// </summary>
    public async Task<AccountRecoveryResult> ConfirmAsync(ConfirmEmailInput input)
    {
        if (string.IsNullOrEmpty(input.UserId) || input.Code is null)
        {
            return new AccountRecoveryResult(false, "Link de confirmação inválido.");
        }

        var user = await _users.FindByIdAsync(input.UserId);
        if (user is null)
        {
            return new AccountRecoveryResult(false, "Utilizador não encontrado.");
        }

        var result = await _users.ConfirmEmailAsync(user, WebUtility.UrlDecode(input.Code));
        return result.Succeeded
            ? new AccountRecoveryResult(true, "Obrigado por confirmar o seu email.")
            : new AccountRecoveryResult(false, "Erro ao confirmar o seu email.");
    }
}
