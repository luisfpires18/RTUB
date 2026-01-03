using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Enums;

/// <summary>
/// Níveis de dificuldade para acordes musicais
/// </summary>
public enum DifficultyLevel
{
    [Display(Name = "Fácil")]
    Easy,

    [Display(Name = "Médio")]
    Medium,

    [Display(Name = "Difícil")]
    Hard
}
