using System.ComponentModel.DataAnnotations;

namespace RTUB.Application.DTOs;

/// <summary>
/// Form model for creating multiple rehearsals in a date range
/// Used in Rehearsals page for bulk rehearsal creation
/// </summary>
public class RehearsalRangeFormModel
{
    [Required(ErrorMessage = "A data de início é obrigatória")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "A data de fim é obrigatória")]
    public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(1);

    [Required(ErrorMessage = "A localização é obrigatória")]
    [MaxLength(200, ErrorMessage = "A localização não pode exceder 200 caracteres")]
    public string Location { get; set; } = "Centro Académico";

    [MaxLength(500, ErrorMessage = "O tema não pode exceder 500 caracteres")]
    public string? Theme { get; set; }

    [MaxLength(1000, ErrorMessage = "A descrição não pode exceder 1000 caracteres")]
    public string? Description { get; set; }
}
