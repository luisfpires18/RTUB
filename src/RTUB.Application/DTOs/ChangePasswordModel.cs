using System.ComponentModel.DataAnnotations;

namespace RTUB.Application.DTOs;

/// <summary>
/// Form model for changing user password.
/// Used in the Profile page for password change operations.
/// </summary>
public class ChangePasswordModel
{
    /// <summary>
    /// The user's current password for verification.
    /// </summary>
    [Required(ErrorMessage = "A palavra-passe atual é obrigatória")]
    [DataType(DataType.Password)]
    public string OldPassword { get; set; } = string.Empty;

    /// <summary>
    /// The new password to set.
    /// </summary>
    [Required(ErrorMessage = "A nova palavra-passe é obrigatória")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "A palavra-passe deve ter pelo menos 8 caracteres")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    /// Confirmation of the new password.
    /// </summary>
    [Required(ErrorMessage = "A confirmação da palavra-passe é obrigatória")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "A nova palavra-passe e a confirmação não coincidem.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
