// Event discussion board: permissions and validation rules.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;

namespace RTUB.Application.Services;

public sealed partial class EventDiscussionBoardService
{
    // ---------- rules ----------

    private static string Me(ClaimsPrincipal user) => EventsAuthorization.UserId(user)!;

    /// <summary>Authors delete their own; the Owner deletes anyone's.</summary>
    private static bool CanDelete(string authorId, ClaimsPrincipal user) =>
        authorId == EventsAuthorization.UserId(user) || EventsAuthorization.IsOwner(user);

    /// <summary>The driver, or Admin/Owner, seats and unseats passengers.</summary>
    private static bool CanSeat(string driverId, ClaimsPrincipal user) =>
        driverId == EventsAuthorization.UserId(user) || EventsAuthorization.CanManage(user);

    private async Task<EventResult<EventDiscussionDto>?> RefusalAsync(int eventId, ClaimsPrincipal user)
    {
        if (EventsAuthorization.UserId(user) is null)
        {
            return Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Events.AnyAsync(e => e.Id == eventId) ? null : Fail(EventResultStatus.NotFound);
    }

    private sealed record ValidPost(string Title, string Body);

    private static ValidPost? ValidatePost(EventPostInput input)
    {
        var body = input.Body?.Trim() ?? string.Empty;
        var title = TitleFor(input.Title, body);
        return PostErrorsFor(input).Count == 0 ? new ValidPost(title, body) : null;
    }

    /// <summary>A blank title is taken from the start of the text (the table requires 3-120 characters).</summary>
    internal static string TitleFor(string? title, string body)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        var text = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= 60 ? text : text[..59].TrimEnd() + "…";
    }

    private static Dictionary<string, string[]> PostErrorsFor(EventPostInput input)
    {
        var errors = new Dictionary<string, string[]>();
        var body = input.Body?.Trim() ?? string.Empty;
        var title = TitleFor(input.Title, body);
        if (body.Length < BodyMin)
        {
            errors["body"] = new[] { $"Escreve pelo menos {BodyMin} caracteres." };
        }
        else if (body.Length > BodyMax)
        {
            errors["body"] = new[] { $"A mensagem não pode exceder {BodyMax} caracteres." };
        }
        else if (title.Length < TitleMin)
        {
            errors["title"] = new[] { $"O título deve ter pelo menos {TitleMin} caracteres." };
        }

        if (title.Length > TitleMax)
        {
            errors["title"] = new[] { $"O título não pode exceder {TitleMax} caracteres." };
        }

        return errors;
    }

    private static EventResult<EventDiscussionDto> PostErrors(EventPostInput input) =>
        new(EventResultStatus.Invalid, Errors: PostErrorsFor(input));

    private static string? CommentError(string? body)
    {
        var text = body?.Trim() ?? string.Empty;
        return text.Length == 0 ? "Escreve o comentário."
            : text.Length > CommentMax ? $"O comentário não pode exceder {CommentMax} caracteres."
            : null;
    }

    private static Dictionary<string, string[]> TransportErrors(EventTransportInput input, int seated)
    {
        var errors = new Dictionary<string, string[]>();
        var vehicle = input.Vehicle?.Trim() ?? string.Empty;
        if (vehicle.Length == 0)
        {
            errors["vehicle"] = new[] { "A descrição da viatura é obrigatória." };
        }
        else if (vehicle.Length > VehicleMax)
        {
            errors["vehicle"] = new[] { $"A descrição não pode exceder {VehicleMax} caracteres." };
        }

        if (input.Seats is < 2 or > 20)
        {
            errors["seats"] = new[] { "O número de lugares deve estar entre 2 e 20." };
        }
        else if (input.Seats < seated)
        {
            errors["seats"] = new[] { $"Já há {seated} pessoas nesta viatura." };
        }

        if ((input.Notes?.Trim().Length ?? 0) > TransportNotesMax)
        {
            errors["notes"] = new[] { $"As notas não podem exceder {TransportNotesMax} caracteres." };
        }

        return errors;
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
