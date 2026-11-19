using System.Globalization;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Meeting board: meeting form and small helpers.
public sealed partial class MeetingBoardService
{
    // ---------- the meeting form ----------

    /// <param name="savedRepresentativeId">The representative already saved on the meeting being edited: it stays valid even
    /// once that member is no longer a plain Tuno.</param>
    private (Meeting? Meeting, Errors Errors) ReadMeeting(
        MeetingInput input, MeetingAccess access, List<ApplicationUser> users, string? savedRepresentativeId)
    {
        var errors = new Errors();
        if (!TryType(input.Type, out var type) || !access.FormTypes.Contains(type))
        {
            errors.Add("type", "Escolha um tipo de reunião da lista.");
        }

        var title = RequiredText(input.Title, "title", "O título é obrigatório.", MaxTitleLength, "O título não pode exceder 200 caracteres.", errors);
        if (!TryDate(input.Date, out var date))
        {
            errors.Add("date", "A data é obrigatória.");
        }

        var location = OptionalText(input.Location, "location", MaxLocationLength, "A localização não pode exceder 200 caracteres.", errors);
        var statement = input.Statement ?? string.Empty;
        if (string.IsNullOrWhiteSpace(statement))
        {
            errors.Add("statement", "A declaração é obrigatória.");
        }
        else if (statement.Length > MaxStatementLength)
        {
            errors.Add("statement", $"A declaração não pode exceder {MaxStatementLength} caracteres.");
        }

        // A Tuno representative only for a CV, and only one the form offered (or the one already saved).
        string? representative = null;
        if (type == MeetingType.ConselhoVeteranos && !string.IsNullOrWhiteSpace(input.TunoRepresentativeId))
        {
            if (input.TunoRepresentativeId == savedRepresentativeId
                || TunoRepresentativeCandidates(users).Any(u => u.Id == input.TunoRepresentativeId))
            {
                representative = input.TunoRepresentativeId;
            }
            else
            {
                errors.Add("tunoRepresentativeId", "Escolha um Tuno da lista.");
            }
        }

        if (errors.Any)
        {
            return (null, errors);
        }

        return (new Meeting
        {
            Type = type,
            Title = title!,
            Date = date,
            Location = location,
            Statement = statement,
            TunoRepresentativeUserId = representative,
        }, errors);
    }

    // ---------- small helpers ----------

    private static MeetingPersonDto Person(ApplicationUser u, Meeting meeting) => new(
        u.Nickname ?? $"{u.FirstName} {u.LastName}",
        u.ProfilePictureSrc,
        meeting.Type == MeetingType.ConselhoVeteranos ? YearsAsTuno(u) : null);

    private static MeetingPersonOptionDto TunoOption(ApplicationUser u) =>
        new(u.Id, $"{u.Nickname ?? $"{u.FirstName} {u.LastName}"} - {YearsAsTuno(u)}");

    /// <summary>"x anos y meses de Tuno", from YearTuno / MonthTuno.</summary>
    public static string YearsAsTuno(ApplicationUser user)
    {
        if (user.YearTuno == null)
        {
            return "< 1 ano de Tuno";
        }

        var now = PortugalTime.Today(TimeProvider.System);
        var start = new DateTime(user.YearTuno.Value, user.MonthTuno ?? 1, 1);
        var totalMonths = ((now.Year - start.Year) * 12) + (now.Month - start.Month);
        if (totalMonths < 1)
        {
            return "< 1 mês de Tuno";
        }

        var years = totalMonths / 12;
        var months = totalMonths % 12;
        if (years == 0)
        {
            return months == 1 ? "1 mês de Tuno" : $"{months} meses de Tuno";
        }

        if (months == 0)
        {
            return years == 1 ? "1 ano de Tuno" : $"{years} anos de Tuno";
        }

        return $"{years} {(years == 1 ? "ano" : "anos")} {months} {(months == 1 ? "mês" : "meses")} de Tuno";
    }

    /// <summary>The ata view's names: First "Nickname" Last.</summary>
    private static string FullName(ApplicationUser user)
    {
        var first = user.FirstName ?? string.Empty;
        var last = user.LastName ?? string.Empty;
        return string.IsNullOrEmpty(user.Nickname) ? $"{first} {last}".Trim() : $"{first} \"{user.Nickname}\" {last}".Trim();
    }

    public static string TypeLabel(MeetingType type) => type switch
    {
        MeetingType.AssembleiaGeralOrdinaria => "Assembleia Geral Ordinária",
        MeetingType.AssembleiaGeralExtraordinaria => "Assembleia Geral Extraordinária",
        MeetingType.ConselhoVeteranos => "Conselho de Veteranos",
        MeetingType.ReuniaoDirecao => "Reunião de Direção",
        _ => string.Empty,
    };

    private static string Kind(MeetingType type) =>
        type == MeetingType.ConselhoVeteranos ? "cv" : MeetingAccess.IsAssembly(type) ? "ag" : "direcao";

    /// <summary>The create / edit form's choices, labelled.</summary>
    private static IReadOnlyList<MeetingTypeOptionDto> TypeOptions(IEnumerable<MeetingType> types) =>
        types.Select(t => new MeetingTypeOptionDto(t.ToString(), t switch
        {
            MeetingType.AssembleiaGeralOrdinaria => "Assembleia Geral Ordinária (AGO)",
            MeetingType.AssembleiaGeralExtraordinaria => "Assembleia Geral Extraordinária (AGE)",
            MeetingType.ConselhoVeteranos => "Conselho de Veteranos (CV)",
            _ => TypeLabel(t),
        })).ToList();

    private static string Format(DateTime value) => value.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static bool TryDate(string? value, out DateTime date) =>
        DateTime.TryParseExact(value?.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryType(string? value, out MeetingType type)
    {
        type = default;
        return !string.IsNullOrWhiteSpace(value)
               && !int.TryParse(value, out _)
               && Enum.TryParse(value, ignoreCase: false, out type)
               && Enum.IsDefined(type);
    }

    /// <summary>
    /// The year filter: null = the current fiscal year, "all" or "" = every year, else "YYYY-YYYY"
    /// (1 September to 31 August).
    /// </summary>
    private static bool TryFiscalYear(string? value, out string? selected, out (DateTime Start, DateTime End)? range)
    {
        selected = null;
        range = null;
        var year = value is null ? FiscalYearHelper.GetCurrentFiscalYearString() : value.Trim();
        if (year.Length == 0 || year == "all")
        {
            return true;
        }

        var parts = year.Split('-');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            || end != start + 1 || start < 2000 || start > 2999)
        {
            return false;
        }

        selected = year;
        range = (new DateTime(start, 9, 1), new DateTime(start + 1, 8, 31));
        return true;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? RequiredText(string? value, string field, string requiredMessage, int max, string tooLongMessage, Errors errors)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            errors.Add(field, requiredMessage);
            return null;
        }

        if (text.Length > max)
        {
            errors.Add(field, tooLongMessage);
        }

        return text;
    }

    private static string? OptionalText(string? value, string field, int max, string tooLongMessage, Errors errors)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Length > max)
        {
            errors.Add(field, tooLongMessage);
        }

        return text;
    }

    private static EventResult<T> Invalid<T>(Errors errors) =>
        new(EventResultStatus.Invalid, Errors: errors.ToDictionary());

    /// <summary>Field errors collected before answering, so a form shows every problem at once.</summary>
    private sealed class Errors
    {
        private readonly Dictionary<string, List<string>> _errors = new();

        public bool Any => _errors.Count > 0;

        public void Add(string field, string message)
        {
            if (!_errors.TryGetValue(field, out var messages))
            {
                _errors[field] = messages = new List<string>();
            }

            messages.Add(message);
        }

        public IReadOnlyDictionary<string, string[]> ToDictionary() => _errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
    }
}
