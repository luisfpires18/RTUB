// Treasury: input checks, receipt checks and delete helpers.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Constants;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class TreasuryService
{
    private static (string, string)? ActivityError(TreasuryActivityInput input)
    {
        var name = input.Name?.Trim() ?? "";
        if (name.Length == 0)
        {
            return ("name", "O nome da atividade é obrigatório.");
        }

        if (name.Length > 200)
        {
            return ("name", "O nome da atividade não pode exceder 200 caracteres.");
        }

        if (input.StartDate is null)
        {
            return ("startDate", "A data da atividade é obrigatória.");
        }

        if (input.EndDate is { } end && end.Date < input.StartDate.Value.Date)
        {
            return ("endDate", "A data de fim não pode ser anterior à data de início.");
        }

        return input.Description?.Trim().Length > 1000 ? ("description", "A descrição não pode exceder 1000 caracteres.") : null;
    }

    private static (string, string)? TransactionError(TreasuryTransactionInput input)
    {
        if (input.Date is null)
        {
            return ("date", "A data é obrigatória.");
        }

        var description = input.Description?.Trim() ?? "";
        if (description.Length is 0 or > 500)
        {
            return ("description", description.Length == 0 ? "A descrição é obrigatória." : "A descrição não pode exceder 500 caracteres.");
        }

        var category = input.Category?.Trim() ?? "";
        if (category.Length is 0 or > 100)
        {
            return ("category", category.Length == 0 ? "A categoria é obrigatória." : "A categoria não pode exceder 100 caracteres.");
        }

        if (input.Amount is not { } amount || amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            return ("amount", "O valor deve ser maior que 0, com até duas casas decimais.");
        }

        if (input.Type is not (TransactionTypes.Income or TransactionTypes.Expense))
        {
            return ("type", "Escolha receita ou despesa.");
        }

        return input.Receipt is { } r ? ReceiptError(r) : null;
    }

    internal static (string, string)? ReceiptError(TreasuryReceiptUpload receipt)
    {
        if (receipt.Length > MaxReceiptBytes)
        {
            return ("receipt", "O ficheiro é demasiado grande. O tamanho máximo é 10MB.");
        }

        var type = receipt.ContentType.ToLowerInvariant();
        return receipt.Length == 0 || !(type == "application/pdf" || type.StartsWith("image/", StringComparison.Ordinal))
            ? ("receipt", "O recibo tem de ser uma imagem (JPG, PNG, etc.) ou um PDF.")
            : null;
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Deletes <paramref name="owners"/> (an activity, or a report with its activities) and <paramref name="transactions"/> in
    /// one database transaction: every row is tracked, so EF deletes transactions, then activities, then the report, and
    /// both SaveChanges of the context (the rows, then their audit log) commit together or roll back together.
    /// Receipts go only after the commit, best-effort: a storage failure never leaves financial rows half deleted.
    /// </summary>
    private async Task DeleteWithTransactionsAsync(Func<ApplicationDbContext, IQueryable<Transaction>> transactions,
        Func<ApplicationDbContext, Task<IEnumerable<object>>> owners)
    {
        List<string> receipts;
        await using (var db = await _contexts.CreateDbContextAsync())
        {
            await using var scope = await db.Database.BeginTransactionAsync();
            var rows = await transactions(db).ToListAsync();
            receipts = rows.Where(t => !string.IsNullOrEmpty(t.ReceiptUrl)).Select(t => t.ReceiptUrl!).ToList();
            db.Transactions.RemoveRange(rows);
            db.RemoveRange(await owners(db));
            await db.SaveChangesAsync();
            await scope.CommitAsync();
        }

        foreach (var url in receipts)
        {
            try
            {
                await _receipts.DeleteReceiptAsync(url); // refuses (and logs) anything this environment does not own
            }
            catch
            {
                // Best-effort: the rows are already gone; a leftover object is only storage.
            }
        }
    }

    /// <summary>The caller's account, or 401 (visitor, deleted account) / 403 (not a treasury member, or not allowed).</summary>
    private async Task<(ApplicationUser? Member, EventResultStatus? Refused)> CallerAsync(ClaimsPrincipal user,
        Func<ApplicationUser, ClaimsPrincipal, bool>? allowed = null)
    {
        if (user.UserId() is not { } id)
        {
            return (null, EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id) is not { } member)
        {
            return (null, EventResultStatus.SignInRequired);
        }

        return TreasuryAuthorization.CanView(member, user) && (allowed is null || allowed(member, user))
            ? (member, null)
            : (null, EventResultStatus.Forbidden);
    }
}
