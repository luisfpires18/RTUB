using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// Form model for naipe content (videos/images)
/// Used in Naipes page for creating/editing naipe content
/// </summary>
public class NaipeContentFormModel
{
    [Required(ErrorMessage = "O instrumento é obrigatório")]
    public InstrumentType InstrumentType { get; set; }

    [Required(ErrorMessage = "O título é obrigatório")]
    [StringLength(200, ErrorMessage = "O título não pode ter mais de 200 caracteres")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "A ordem é obrigatória")]
    [Range(0.1, 999.9, ErrorMessage = "A ordem deve estar entre 0.1 e 999.9")]
    public decimal SortOrder { get; set; }
}
