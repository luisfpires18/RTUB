using RTUB.Core.Enums;
using RTUB.Core.Entities;
using RTUB.Application.Extensions;

namespace RTUB.Application.Helpers;

/// <summary>
/// Helper methods for status formatting and translation
/// </summary>
public static class StatusHelper
{
    /// <summary>
    /// Gets the Bootstrap badge CSS class for a request status
    /// </summary>
    public static string GetStatusBadgeClass(RequestStatus status)
    {
        return status switch
        {
            RequestStatus.Pending => "bg-warning text-dark",
            RequestStatus.Analysing => "bg-info text-dark",
            RequestStatus.Confirmed => "bg-success",
            RequestStatus.Rejected => "bg-danger",
            _ => "bg-secondary"
        };
    }

    /// <summary>
    /// Gets the Portuguese translation for a request status
    /// </summary>
    public static string GetStatusTranslation(RequestStatus status)
    {
        return status switch
        {
            RequestStatus.Pending => "Pendente",
            RequestStatus.Analysing => "Em Análise",
            RequestStatus.Confirmed => "Confirmado",
            RequestStatus.Rejected => "Rejeitado",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// Gets the lowercase Portuguese translation for filtering text
    /// </summary>
    public static string GetFilterStatusText(RequestStatus? status)
    {
        if (status == null) return "";
        
        return status switch
        {
            RequestStatus.Pending => "pendente",
            RequestStatus.Analysing => "em análise",
            RequestStatus.Confirmed => "confirmado",
            RequestStatus.Rejected => "rejeitado",
            _ => ""
        };
    }

    /// <summary>
    /// Gets a localized display name for an instrument type.
    /// </summary>
    public static string GetInstrumentDisplay(InstrumentType instrument)
    {
        return instrument switch
        {
            InstrumentType.Guitarra => "Guitarra",
            InstrumentType.Bandolim => "Bandolim",
            InstrumentType.Cavaquinho => "Cavaquinho",
            InstrumentType.Acordeao => "Acordeão",
            InstrumentType.Fagote => "Fagote",
            InstrumentType.Flauta => "Flauta",
            InstrumentType.Baixo => "Baixo",
            InstrumentType.Percussao => "Percussão",
            InstrumentType.Pandeireta => "Pandeireta",
            InstrumentType.Estandarte => "Estandarte",
            _ => instrument.ToString()
        };
    }

    /// <summary>
    /// Gets the display text for a member position
    /// </summary>
    public static string GetPositionDisplay(Position position)
    {
        return position switch
        {
            Position.Magister => "MAGISTER",
            Position.ViceMagister => "VICE-MAGISTER",
            Position.Secretario => "SECRETÁRIO",
            Position.PrimeiroTesoureiro => "1º TESOUREIRO",
            Position.SegundoTesoureiro => "2º TESOUREIRO",
            Position.PresidenteMesaAssembleia => "PRESIDENTE MESA ASSEMBLEIA",
            Position.PrimeiroSecretarioMesaAssembleia => "1º SECRETÁRIO MESA ASSEMBLEIA",
            Position.SegundoSecretarioMesaAssembleia => "2º SECRETÁRIO MESA ASSEMBLEIA",
            Position.PresidenteConselhoFiscal => "PRESIDENTE CONSELHO FISCAL",
            Position.PrimeiroRelatorConselhoFiscal => "1º RELATOR CONSELHO FISCAL",
            Position.SegundoRelatorConselhoFiscal => "2º RELATOR CONSELHO FISCAL",
            Position.PresidenteConselhoVeteranos => "PRESIDENTE CONSELHO VETERANOS",
            Position.Ensaiador => "ENSAIADOR",
            _ => position.ToString()
        };
    }

    /// <summary>
    /// Gets the display text for a member category
    /// </summary>
    public static string GetCategoryDisplay(MemberCategory category)
    {
        return category switch
        {
            MemberCategory.Tuno => "TUNO",
            MemberCategory.Veterano => "VETERANO",
            MemberCategory.Tunossauro => "TUNOSSAURO",
            MemberCategory.TunoHonorario => "TUNO HONORÁRIO",
            MemberCategory.Fundador => "FUNDADOR",
            MemberCategory.Caloiro => "CALOIRO",
            MemberCategory.Leitao => "LEITÃO",
            _ => category.ToString()
        };
    }

    /// <summary>
    /// Gets the Bootstrap badge CSS class for a member category
    /// </summary>
    public static string GetCategoryBadgeClass(MemberCategory category)
    {
        return category switch
        {
            MemberCategory.Tuno => "badge-tuno",
            MemberCategory.Veterano => "badge-veterano",
            MemberCategory.Tunossauro => "badge-tunossauro",
            MemberCategory.TunoHonorario => "badge-honorario",
            MemberCategory.Fundador => "badge-fundador",
            MemberCategory.Caloiro => "badge-caloiro",
            MemberCategory.Leitao => "badge-leitao",
            _ => "bg-secondary"
        };
    }

    /// <summary>
    /// Gets the display categories for a user, automatically adding Veterano/Tunossauro based on years as Tuno
    /// </summary>
    public static List<MemberCategory> GetDisplayCategories(ApplicationUser user)
    {
        var displayCategories = new List<MemberCategory>(user.Categories);

        // If user is TUNO, automatically add VETERANO and/or TUNOSSAURO based on years
        #pragma warning disable CS0618 // Type or member is obsolete - entity check is appropriate here
        if (user.IsTuno() && user.YearTuno.HasValue)
        #pragma warning restore CS0618
        {
            if (user.QualifiesForTunossauro())
            {
                // 6+ years: show TUNO VETERANO TUNOSSAURO
                if (!displayCategories.Contains(MemberCategory.Veterano))
                    displayCategories.Add(MemberCategory.Veterano);
                if (!displayCategories.Contains(MemberCategory.Tunossauro))
                    displayCategories.Add(MemberCategory.Tunossauro);
            }
            else if (user.QualifiesForVeterano())
            {
                // 2-5 years: show TUNO VETERANO
                if (!displayCategories.Contains(MemberCategory.Veterano))
                    displayCategories.Add(MemberCategory.Veterano);
            }
        }

        return displayCategories.Distinct().ToList();
    }
    
    /// <summary>
    /// Gets the display categories for a user, automatically adding Veterano/Tunossauro based on years as Tuno
    /// Legacy overload for backward compatibility
    /// </summary>
    public static List<MemberCategory> GetDisplayCategories(List<MemberCategory> categories, int? yearTuno)
    {
        var user = new ApplicationUser { YearTuno = yearTuno };
        user.Categories = categories;
        return GetDisplayCategories(user);
    }

    /// <summary>
    /// Gets the Portuguese display name for an event type
    /// </summary>
    public static string GetEventTypeDisplay(EventType eventType)
    {
        return eventType switch
        {
            EventType.Festival => "Festival",
            EventType.Atuacao => "Atuação",
            EventType.Casamento => "Casamento",
            EventType.Serenata => "Serenata",
            EventType.Arraial => "Arraial",
            EventType.Convivio => "Convívio",
            EventType.Nerba => "Nerba",
            EventType.Missa => "Missa",
            EventType.Batizado => "Batizado",
            _ => eventType.ToString()
        };
    }
}
