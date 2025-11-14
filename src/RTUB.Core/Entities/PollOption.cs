using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents an option within a poll
/// </summary>
public class PollOption : BaseEntity
{
    [Required(ErrorMessage = "O texto da opção é obrigatório")]
    [MaxLength(200, ErrorMessage = "O texto da opção não pode exceder 200 caracteres")]
    public string Text { get; set; } = string.Empty;
    
    [Required]
    public int PollId { get; set; }
    
    // Navigation properties
    public virtual Poll? Poll { get; set; }
    public virtual ICollection<PollVote> Votes { get; set; } = new List<PollVote>();
    
    // Private constructor for EF Core
    private PollOption() { }
    
    public static PollOption Create(string text, int pollId)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("O texto da opção não pode estar vazio", nameof(text));
        
        if (pollId <= 0)
            throw new ArgumentException("O ID da votação é inválido", nameof(pollId));
        
        return new PollOption
        {
            Text = text,
            PollId = pollId
        };
    }
    
    public void UpdateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("O texto da opção não pode estar vazio", nameof(text));
        
        Text = text;
    }
}
