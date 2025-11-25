using System.ComponentModel.DataAnnotations;

namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for sending a new message
/// </summary>
public class SendMessageDto
{
    [Required(ErrorMessage = "O destinatário é obrigatório")]
    public string ReceiverId { get; set; } = string.Empty;

    [Required(ErrorMessage = "A mensagem não pode estar vazia")]
    [StringLength(2000, ErrorMessage = "A mensagem não pode ter mais de 2000 caracteres")]
    public string Body { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public string? Link { get; set; }
}
