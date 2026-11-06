using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Task 076 (#257): which member-information fields apply to a member, and which of them are missing - the one rule for
/// the Mod's limited edit (<see cref="MemberAdminService.UpdateInfoAsync"/>) and the "Membros" queue of "As minhas ações
/// pendentes". It follows the member form's existing rules (<c>MemberAdminService.ApplySpecialCategories</c>):
/// - Cidade, Contacto: everyone;
/// - Instrumento: everyone but a Tuno Honorário (an honorary distinction, not a playing member);
/// - Entrada na Tuna (Leitão date) and Padrinho: Leitão, Caloiro and Tuno, except Fundador and Tuno Honorário (they have
///   neither);
/// - passagem a Caloiro: Caloiro and Tuno, same exceptions - a Leitão is never asked for it early;
/// - passagem a Tuno: Tuno, except Fundador (always December 1991).
/// A date counts as filled when its year is (old data has year-only pairs, which the form keeps).
/// </summary>
public static class MemberInfoRules
{
    public const string City = "city";
    public const string Phone = "phone";
    public const string Instrument = "instrument";
    public const string Leitao = "leitao";
    public const string Caloiro = "caloiro";
    public const string Tuno = "tuno";
    public const string Mentor = "mentor";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [City] = "Cidade", [Phone] = "Contacto", [Instrument] = "Instrumento", [Leitao] = "Entrada na Tuna",
        [Caloiro] = "Passagem a Caloiro", [Tuno] = "Passagem a Tuno", [Mentor] = "Padrinho",
    };

    /// <summary>A member the queue covers: not expelled, with a Leitão / Caloiro / Tuno category.</summary>
    public static bool InScope(ApplicationUser u) =>
        !u.IsExpelled && u.Categories.Any(c => c is MemberCategory.Leitao or MemberCategory.Caloiro or MemberCategory.Tuno);

    public static IReadOnlyList<string> Applicable(IReadOnlyCollection<MemberCategory> categories)
    {
        var fundador = categories.Contains(MemberCategory.Fundador);
        var honorario = categories.Contains(MemberCategory.TunoHonorario);
        var special = fundador || honorario;
        var tuno = categories.Contains(MemberCategory.Tuno);
        var caloiro = categories.Contains(MemberCategory.Caloiro);
        var fields = new List<string> { City, Phone };
        if (!honorario) fields.Add(Instrument);
        if (!special) fields.Add(Leitao);
        if (!special && (caloiro || tuno)) fields.Add(Caloiro);
        if (tuno && !fundador) fields.Add(Tuno);
        if (!special) fields.Add(Mentor);
        return fields;
    }

    public static IReadOnlyList<string> Missing(ApplicationUser u, bool hasInstrument) =>
        Applicable(u.Categories).Where(f => f switch
        {
            City => string.IsNullOrWhiteSpace(u.City),
            Phone => string.IsNullOrWhiteSpace(u.PhoneNumber),
            Instrument => !hasInstrument,
            Leitao => u.YearLeitao is null,
            Caloiro => u.YearCaloiro is null,
            Tuno => u.YearTuno is null,
            Mentor => string.IsNullOrEmpty(u.MentorId),
            _ => false,
        }).ToList();
}
