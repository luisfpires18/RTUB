using System.ComponentModel.DataAnnotations;

namespace RTUB.Contracts.Rehearsals;

/// <summary>
/// DTO for canceling a rehearsal
/// </summary>
public class CancelRehearsalDto
{
    [Required(ErrorMessage = "O motivo de cancelamento é obrigatório")]
    [MaxLength(1000, ErrorMessage = "O motivo de cancelamento não pode exceder 1000 caracteres")]
    public string Reason { get; set; } = string.Empty;
}
