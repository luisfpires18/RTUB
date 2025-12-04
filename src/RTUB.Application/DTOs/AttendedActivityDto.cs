namespace RTUB.Application.DTOs;

/// <summary>
/// DTO representing a single attended activity (event or rehearsal)
/// Used for displaying detailed attended events list in leaderboard modal
/// </summary>
public class AttendedActivityDto
{
    /// <summary>
    /// Date of the activity
    /// </summary>
    public DateTime Date { get; set; }
    
    /// <summary>
    /// Name of the activity (event name or "Ensaio" for rehearsals)
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of activity (Festival, Atuacao, Ensaio, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// XP awarded for this specific activity
    /// </summary>
    public int XpEarned { get; set; }
    
    /// <summary>
    /// Whether this is a rehearsal (true) or an event (false)
    /// </summary>
    public bool IsRehearsal { get; set; }
}
