using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents an attachment associated with a meeting Ata
/// </summary>
public class MeetingAtaAttachment : BaseEntity
{
    [Required(ErrorMessage = "A Ata é obrigatória")]
    public int MeetingAtaId { get; set; }

    public MeetingAta MeetingAta { get; set; } = null!;

    [Required(ErrorMessage = "O tipo de anexo é obrigatório")]
    [MaxLength(100, ErrorMessage = "O tipo de anexo não pode exceder 100 caracteres")]
    public string AttachmentType { get; set; } = string.Empty;

    [Required(ErrorMessage = "O nome é obrigatório")]
    [MaxLength(200, ErrorMessage = "O nome não pode exceder 200 caracteres")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "A descrição não pode exceder 1000 caracteres")]
    public string? Description { get; set; }

    [MaxLength(500, ErrorMessage = "A URL do ficheiro não pode exceder 500 caracteres")]
    public string? FileUrl { get; set; }

    [Required(ErrorMessage = "A inclusão no PDF é obrigatória")]
    public bool IncludeInPdf { get; set; }
}
