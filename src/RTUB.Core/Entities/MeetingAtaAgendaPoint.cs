using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents an agenda point within a meeting Ata
/// </summary>
public class MeetingAtaAgendaPoint : BaseEntity
{
    [Required(ErrorMessage = "A Ata é obrigatória")]
    public int MeetingAtaId { get; set; }

    public MeetingAta MeetingAta { get; set; } = null!;

    [Required(ErrorMessage = "O número do ponto é obrigatório")]
    public int PointNumber { get; set; }

    [Required(ErrorMessage = "O título é obrigatório")]
    [MaxLength(500, ErrorMessage = "O título não pode exceder 500 caracteres")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(5000, ErrorMessage = "O resumo da discussão não pode exceder 5000 caracteres")]
    public string? DiscussionSummary { get; set; }

    [MaxLength(5000, ErrorMessage = "O texto da decisão não pode exceder 5000 caracteres")]
    public string? DecisionText { get; set; }

    public int? VotesFor { get; set; }

    public int? VotesAgainst { get; set; }

    public int? VotesAbstain { get; set; }

    [MaxLength(100, ErrorMessage = "O resultado da votação não pode exceder 100 caracteres")]
    public string? VoteResult { get; set; }
}
