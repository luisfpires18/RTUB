using System.ComponentModel.DataAnnotations;

namespace RTUB.Application.DTOs;

/// <summary>
/// Form model for creating new questions
/// Used in Questions page for question management
/// </summary>
public class NewQuestionModel
{
    [Required(ErrorMessage = "O título é obrigatório")]
    [MaxLength(100, ErrorMessage = "O título deve ter no máximo 100 caracteres")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "A pergunta é obrigatória")]
    [MinLength(10, ErrorMessage = "A pergunta deve ter pelo menos 10 caracteres")]
    public string Content { get; set; } = string.Empty;
}
