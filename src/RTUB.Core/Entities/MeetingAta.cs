using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents the Ata (minutes) of a meeting
/// </summary>
public class MeetingAta : BaseEntity
{
    [Required(ErrorMessage = "A reunião é obrigatória")]
    public int MeetingId { get; set; }

    public Meeting Meeting { get; set; } = null!;

    [MaxLength(50, ErrorMessage = "O número da Ata não pode exceder 50 caracteres")]
    public string? AtaNumber { get; set; }

    [Required(ErrorMessage = "A hora de início real é obrigatória")]
    public DateTime ActualStartTime { get; set; }

    public DateTime? ActualEndTime { get; set; }

    [Required(ErrorMessage = "A localização é obrigatória")]
    [MaxLength(200, ErrorMessage = "A localização não pode exceder 200 caracteres")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "O presidente é obrigatório")]
    public string PresidentUserId { get; set; } = string.Empty;

    public ApplicationUser PresidentUser { get; set; } = null!;

    // First secretary is optional (for CV meetings, user selects from enrolled members; for AG, uses role-based default)
    public string? FirstSecretaryUserId { get; set; }

    public ApplicationUser? FirstSecretaryUser { get; set; }

    public string? SecondSecretaryUserId { get; set; }

    public ApplicationUser? SecondSecretaryUser { get; set; }

    [Required(ErrorMessage = "A base de quórum é obrigatória")]
    [MaxLength(100, ErrorMessage = "A base de quórum não pode exceder 100 caracteres")]
    public string QuorumBasis { get; set; } = string.Empty;

    [Required(ErrorMessage = "Os presentes são obrigatórios")]
    public string AttendeesPresent { get; set; } = string.Empty;

    [Required(ErrorMessage = "Os ausentes são obrigatórios")]
    public string AttendeesAbsent { get; set; } = string.Empty;

    [MaxLength(5000, ErrorMessage = "O texto de encerramento não pode exceder 5000 caracteres")]
    public string? ClosingText { get; set; }

    [Required(ErrorMessage = "O estado é obrigatório")]
    public MeetingAtaStatus Status { get; set; } = MeetingAtaStatus.Draft;

    public DateTime? GeneratedAt { get; set; }

    [MaxLength(500, ErrorMessage = "A URL de armazenamento do PDF não pode exceder 500 caracteres")]
    public string? PdfStorageUrl { get; set; }

    // Navigation properties
    public List<MeetingAtaAgendaPoint> AgendaPoints { get; set; } = new();
    public List<MeetingAtaAttachment> Attachments { get; set; } = new();
    public List<MeetingAtaConfirmation> Confirmations { get; set; } = new();
}
