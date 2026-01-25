using System.ComponentModel.DataAnnotations;

namespace RTUB.Application.DTOs;

/// <summary>
/// Form model for updating a member's nickname
/// Used in Members page for nickname editing
/// </summary>
public class NicknameFormModel
{
    /// <summary>
    /// The nickname value
    /// </summary>
    [Required(ErrorMessage = "O nome de tuna é obrigatório")]
    [MaxLength(80, ErrorMessage = "O nome de tuna não pode exceder 80 caracteres")]
    public string? Nickname { get; set; }
}
