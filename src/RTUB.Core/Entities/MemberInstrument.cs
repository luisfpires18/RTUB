using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a musical instrument played by a member
/// Members can have multiple instruments with one marked as primary
/// </summary>
public class MemberInstrument : BaseEntity
{
    [Required(ErrorMessage = "O ID do utilizador é obrigatório")]
    public string MemberId { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "O tipo de instrumento é obrigatório")]
    public InstrumentType InstrumentType { get; set; }
    
    public bool IsPrimary { get; set; } = false;
    
    // Navigation property
    public virtual ApplicationUser? Member { get; set; }

    // Private constructor for EF Core
    private MemberInstrument() { }

    /// <summary>
    /// Factory method to create a new MemberInstrument
    /// </summary>
    public static MemberInstrument Create(string memberId, InstrumentType instrumentType, bool isPrimary = false)
    {
        if (string.IsNullOrWhiteSpace(memberId))
            throw new ArgumentException("O ID do membro não pode estar vazio", nameof(memberId));

        return new MemberInstrument
        {
            MemberId = memberId,
            InstrumentType = instrumentType,
            IsPrimary = isPrimary
        };
    }

    /// <summary>
    /// Mark this instrument as primary
    /// </summary>
    public void MarkAsPrimary()
    {
        IsPrimary = true;
    }

    /// <summary>
    /// Unmark this instrument as primary
    /// </summary>
    public void UnmarkAsPrimary()
    {
        IsPrimary = false;
    }
}
