namespace RTUB.Core.Entities;

/// <summary>
/// Join table: which special attacks a character has equipped / unlocked.
/// Characters can have multiple spells equipped at once.
/// </summary>
public class CharacterSpecialAttack
{
    public int Id { get; set; }
    public int CharacterId { get; set; }
    public int SpecialAttackId { get; set; }

    /// <summary>
    /// Sort order in the spell bar (player can reorder)
    /// </summary>
    public int SlotIndex { get; set; }

    // Navigation
    public virtual Character Character { get; set; } = null!;
    public virtual SpecialAttack SpecialAttack { get; set; } = null!;
}
