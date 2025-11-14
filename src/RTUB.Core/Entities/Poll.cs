using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a poll for democratic decision-making within the Tuna
/// </summary>
public class Poll : BaseEntity
{
    [Required(ErrorMessage = "O título da votação é obrigatório")]
    [MaxLength(200, ErrorMessage = "O título não pode exceder 200 caracteres")]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000, ErrorMessage = "A descrição não pode exceder 1000 caracteres")]
    public string? Description { get; set; }
    
    [Required]
    public PollType PollType { get; set; } = PollType.SingleChoice;
    
    [Required]
    public PollStatus Status { get; set; } = PollStatus.Active;
    
    public DateTime? StartDate { get; set; }
    
    public DateTime? EndDate { get; set; }
    
    public bool IsAnonymous { get; set; } = false;
    
    [Range(1, 100, ErrorMessage = "O número máximo de votos deve estar entre 1 e 100")]
    public int MaxVotesPerUser { get; set; } = 1;
    
    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;
    
    // Navigation properties
    public virtual ApplicationUser? CreatedByUser { get; set; }
    public virtual ICollection<PollOption> Options { get; set; } = new List<PollOption>();
    public virtual ICollection<PollVote> Votes { get; set; } = new List<PollVote>();
    
    // Private constructor for EF Core
    private Poll() { }
    
    public static Poll Create(string title, string createdByUserId, PollType pollType = PollType.SingleChoice)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da votação não pode estar vazio", nameof(title));
        
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("O ID do criador é obrigatório", nameof(createdByUserId));
        
        return new Poll
        {
            Title = title,
            CreatedByUserId = createdByUserId,
            PollType = pollType,
            Status = PollStatus.Active,
            MaxVotesPerUser = pollType == PollType.SingleChoice ? 1 : 3
        };
    }
    
    public void UpdateDetails(string title, string? description, DateTime? startDate, DateTime? endDate, bool isAnonymous, int maxVotesPerUser)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da votação não pode estar vazio", nameof(title));
        
        if (maxVotesPerUser < 1 || maxVotesPerUser > 100)
            throw new ArgumentException("O número máximo de votos deve estar entre 1 e 100", nameof(maxVotesPerUser));
        
        Title = title;
        Description = description;
        StartDate = startDate;
        EndDate = endDate;
        IsAnonymous = isAnonymous;
        MaxVotesPerUser = maxVotesPerUser;
    }
    
    public void SetStatus(PollStatus status)
    {
        Status = status;
    }
    
    public void Close()
    {
        Status = PollStatus.Closed;
    }
    
    public bool IsActive()
    {
        if (Status != PollStatus.Active)
            return false;
        
        var now = DateTime.UtcNow;
        
        if (StartDate.HasValue && now < StartDate.Value)
            return false;
        
        if (EndDate.HasValue && now > EndDate.Value)
            return false;
        
        return true;
    }
}
