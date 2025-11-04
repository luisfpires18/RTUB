using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents an audit log entry tracking changes to entities
/// </summary>
public class AuditLog
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;
    
    public int? EntityId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;
    
    [MaxLength(256)]
    public string? UserId { get; set; }
    
    [MaxLength(256)]
    public string? UserName { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public string? Changes { get; set; }
    
    public bool IsCriticalAction { get; set; }
}
