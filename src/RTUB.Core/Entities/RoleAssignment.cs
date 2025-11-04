using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a role assignment for a specific fiscal year
/// Tracks which member held which position during which period
/// </summary>
public class RoleAssignment : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public Position Position { get; set; }
    public int StartYear { get; set; }
    public int EndYear { get; set; }
    public string? Notes { get; set; }
    
    // Navigation property
    public virtual ApplicationUser? User { get; set; }

    // Private constructor for EF Core
    public RoleAssignment() { }

    public static RoleAssignment Create(string userId, Position position, int startYear, int endYear, string? notes = null, string? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        
        if (startYear >= endYear)
            throw new ArgumentException("End year must be after start year");

        return new RoleAssignment
        {
            UserId = userId,
            Position = position,
            StartYear = startYear,
            EndYear = endYear,
            Notes = notes,
            CreatedBy = createdBy
        };
    }

    public void UpdateDetails(Position position, int startYear, int endYear, string? notes)
    {
        if (startYear >= endYear)
            throw new ArgumentException("End year must be after start year");

        Position = position;
        StartYear = startYear;
        EndYear = endYear;
        Notes = notes;
    }

    public string GetFiscalYear()
    {
        return $"{StartYear}-{EndYear}";
    }
    
    // Property alias for backward compatibility
    public string FiscalYear => GetFiscalYear();
}
