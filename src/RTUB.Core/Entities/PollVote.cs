using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a user's vote on a poll option
/// </summary>
public class PollVote : BaseEntity
{
    /// <summary>
    /// User ID (nullable for anonymous votes)
    /// </summary>
    public string? UserId { get; set; }
    
    [Required]
    public int PollOptionId { get; set; }
    
    [Required]
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
    public virtual PollOption? PollOption { get; set; }
    
    // Private constructor for EF Core
    private PollVote() { }
    
    public static PollVote Create(int pollOptionId, string? userId = null)
    {
        if (pollOptionId <= 0)
            throw new ArgumentException("O ID da opção é inválido", nameof(pollOptionId));
        
        return new PollVote
        {
            PollOptionId = pollOptionId,
            UserId = userId,
            VotedAt = DateTime.UtcNow
        };
    }
}
