using System.ComponentModel.DataAnnotations;

namespace RTUB.Application.DTOs;

/// <summary>
/// Form model for creating/editing a single rehearsal
/// Used in Rehearsals page for rehearsal management
/// </summary>
public class RehearsalFormModel
{
    [Required(ErrorMessage = "A data é obrigatória")]
    public DateTime Date { get; set; } = DateTime.Today.AddDays(1);

    [Required(ErrorMessage = "A localização é obrigatória")]
    [MaxLength(200, ErrorMessage = "A localização não pode exceder 200 caracteres")]
    public string Location { get; set; } = "Centro Académico";

    [MaxLength(500, ErrorMessage = "O tema não pode exceder 500 caracteres")]
    public string? Theme { get; set; }

    [MaxLength(1000, ErrorMessage = "A descrição não pode exceder 1000 caracteres")]
    public string? Description { get; set; }

    [MaxLength(1000, ErrorMessage = "As notas não podem exceder 1000 caracteres")]
    public string? Notes { get; set; }
}
