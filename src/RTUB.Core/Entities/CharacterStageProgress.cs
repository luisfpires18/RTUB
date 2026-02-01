using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Tracks character progress through stages
/// One record per character per stage
/// </summary>
public class CharacterStageProgress : BaseEntity
{
    /// <summary>
    /// Character ID
    /// </summary>
    [Required]
    public int CharacterId { get; set; }
    
    /// <summary>
    /// Navigation property to Character
    /// </summary>
    public virtual Character Character { get; set; } = null!;
    
    /// <summary>
    /// Stage ID
    /// </summary>
    [Required]
    public int StageId { get; set; }
    
    /// <summary>
    /// Navigation property to Stage
    /// </summary>
    public virtual Stage Stage { get; set; } = null!;
    
    /// <summary>
    /// Number of times this stage has been completed
    /// </summary>
    [Required]
    public int CompletionCount { get; set; }
    
    /// <summary>
    /// First completion date (for tracking achievements)
    /// </summary>
    public DateTime? FirstCompletedAt { get; set; }
    
    /// <summary>
    /// Last completion date
    /// </summary>
    public DateTime? LastCompletedAt { get; set; }
    
    /// <summary>
    /// Has the instrument reward been claimed?
    /// </summary>
    [Required]
    public bool InstrumentClaimed { get; set; }

    // Private constructor for EF Core
    private CharacterStageProgress() { }

    /// <summary>
    /// Factory method to create a new character stage progress record
    /// </summary>
    public static CharacterStageProgress Create(int characterId, int stageId)
    {
        if (characterId <= 0)
            throw new ArgumentException("Character ID must be greater than 0", nameof(characterId));
        if (stageId <= 0)
            throw new ArgumentException("Stage ID must be greater than 0", nameof(stageId));

        return new CharacterStageProgress
        {
            CharacterId = characterId,
            StageId = stageId,
            CompletionCount = 0,
            InstrumentClaimed = false,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Marks the stage as completed and updates timestamps
    /// </summary>
    public void MarkCompleted()
    {
        CompletionCount++;
        LastCompletedAt = DateTime.UtcNow;
        
        if (FirstCompletedAt == null)
        {
            FirstCompletedAt = DateTime.UtcNow;
        }
        
        UpdatedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Marks the instrument reward as claimed
    /// </summary>
    public void ClaimInstrument()
    {
        InstrumentClaimed = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
