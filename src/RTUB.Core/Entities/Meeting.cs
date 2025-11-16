using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a meeting (Assembleia or Conselho)
/// </summary>
public class Meeting : BaseEntity
{
    [Required(ErrorMessage = "O tipo de reunião é obrigatório")]
    public MeetingType Type { get; set; }
    
    [Required(ErrorMessage = "O título é obrigatório")]
    [MaxLength(200, ErrorMessage = "O título não pode exceder 200 caracteres")]
    public string Title { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "A data é obrigatória")]
    public DateTime Date { get; set; }
    
    [MaxLength(200, ErrorMessage = "A localização não pode exceder 200 caracteres")]
    public string? Location { get; set; }
    
    [Required(ErrorMessage = "A declaração é obrigatória")]
    [MaxLength(5000, ErrorMessage = "A declaração não pode exceder 5000 caracteres")]
    public string Statement { get; set; } = string.Empty;
}
