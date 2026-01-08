using System.ComponentModel.DataAnnotations;

namespace RTUB.Application.DTOs;

/// <summary>
/// Form model for member nickname editing.
/// Used in the Members page for updating a member's tuna nickname.
/// </summary>
public class NicknameFormModel
{
    /// <summary>
    /// The member's nickname in the tuna.
    /// </summary>
    [Required(ErrorMessage = "O nome de tuna é obrigatório")]
    [MaxLength(80, ErrorMessage = "O nome de tuna não pode exceder 80 caracteres")]
    public string? Nickname { get; set; }
}
