using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// Pending actions: dismissing and answering an action.

public sealed partial class PendingActionsService
{
    public async Task<EventResult<bool>> DismissAsync(ClaimsPrincipal user, PendingActionDismissInput input)
    {
        if (EventsAuthorization.UserId(user) is not { } userId)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        var kind = input.Kind?.Trim() ?? "";
        var sourceKey = input.SourceKey?.Trim() ?? "";
        if (kind.Length is 0 or > 64 || sourceKey.Length > 64)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await LoadMemberAsync(db, userId) is not { } member)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        // Only an action that is pending for this member now, from a panel that applies to them: whatever the request says,
        // the row (and whether it is required or optional) is the server's.
        foreach (var source in _sources.Where(s => Applies(s, member)))
        {
            if (await source.FindAsync(db, member, kind, sourceKey) is not { } found)
            {
                continue;
            }

            if (found.Row.Required || !found.Row.Dismissible)
            {
                return EventResult<bool>.Fail(EventResultStatus.Forbidden);
            }

            var exists = await db.PendingActionDismissals.AnyAsync(d =>
                d.UserId == userId && d.ActionKind == kind && d.SourceKey == sourceKey && d.Period == found.Period);
            if (!exists)
            {
                db.PendingActionDismissals.Add(new PendingActionDismissal
                {
                    UserId = userId, ActionKind = kind, SourceKey = sourceKey, Period = found.Period,
                    DismissedAt = _time.GetUtcNow().UtcDateTime,
                });
                try
                {
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // A concurrent click stored the same row (unique index): the outcome is the same.
                }
            }

            return EventResult<bool>.Ok(true);
        }

        return EventResult<bool>.Fail(EventResultStatus.NotFound);
    }

    public async Task<EventResult<bool>> RespondAsync(ClaimsPrincipal user, PendingActionResponseInput input)
    {
        if (EventsAuthorization.UserId(user) is not { } userId)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        var kind = input.Kind?.Trim() ?? "";
        var sourceKey = input.SourceKey?.Trim() ?? "";
        var answer = input.Answer?.Trim() ?? "";

        await using var db = await _contexts.CreateDbContextAsync();
        if (await LoadMemberAsync(db, userId) is not { } member)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        // Task 073: commands (Concluir, Fechar questão, 074: Tesouraria, Secretaria) go to their own source; the rest are the
        // attendance answers below. A command of a panel that does not apply to the member (no position, no longer in
        // office) is a 404, like the panel itself.
        if (_sources.OfType<IPendingActionCommand>().FirstOrDefault(c => c.Kind == kind) is { } command)
        {
            if (!Applies((IPendingActionSource)command, member))
            {
                return EventResult<bool>.Fail(EventResultStatus.NotFound);
            }

            if (!command.Answers.Contains(answer))
            {
                return EventResult<bool>.Invalid("answer", "Resposta inválida.");
            }

            // Each command checks its own key ("" is the Secretaria's single row).
            return sourceKey.Length > 64
                ? EventResult<bool>.Fail(EventResultStatus.NotFound)
                : await command.RunAsync(db, member, sourceKey, answer);
        }

        var yes = answer switch { "yes" => true, "no" => false, _ => (bool?)null };
        if (yes is null)
        {
            return EventResult<bool>.Invalid("answer", "Resposta inválida.");
        }

        var responder = _sources.Where(s => Applies(s, member)).OfType<IPendingActionResponder>().FirstOrDefault(r => r.Kind == kind);
        if (responder is null || sourceKey.Length is 0 or > 64)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        // As on the event and rehearsal pages: a note either way (a reason for missing it, or one for going); only someone who
        // goes plays, and the instrument must be one the page offered (none named = not playing).
        var notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        if (notes?.Length > responder.NotesMax)
        {
            return EventResult<bool>.Invalid("notes", $"A nota não pode exceder {responder.NotesMax} caracteres.");
        }

        InstrumentType? instrument = null;
        string? others = null;
        if (yes.Value && !string.IsNullOrWhiteSpace(input.Instrument))
        {
            var (options, _) = await InstrumentOptionsAsync(db, member);
            if (!Enum.TryParse<InstrumentType>(input.Instrument, out var offered) || !Enum.IsDefined(offered) || !options.Contains(offered))
            {
                return EventResult<bool>.Invalid("instrument", "Escolha um dos instrumentos indicados.");
            }

            instrument = offered;
            // As the old pages did: the member's other instruments, by display name.
            others = string.Join(", ", (await RegisteredAsync(db, member.UserId)).Where(i => i != offered).Select(StatusHelper.GetInstrumentDisplay));
            others = others.Length == 0 ? null : others;
        }

        return await responder.RespondAsync(db, member, sourceKey, yes.Value, instrument, others, notes);
    }
}
