using Microsoft.EntityFrameworkCore;
using RTUB.Core.Constants;
using RTUB.Core.Entities;

namespace RTUB.Application.Data;

// Audit switches: which entities are audited.

public partial class ApplicationDbContext
{
    /// <summary>
    /// Whether the entry will become an audit row in this save: added or deleted, or modified with a real change, and
    /// not of an excluded type. Only these may trigger the name lookup.
    /// </summary>
    private static bool WillBeAudited(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<BaseEntity> entry)
    {
        var type = entry.Entity.GetType().Name;
        return !AuditConfiguration.ExcludedEntityTypes.Contains(type) && entry.State switch
        {
            EntityState.Added or EntityState.Deleted => true,
            EntityState.Modified => !AuditConfiguration.ModifiedExcludedEntityTypes.Contains(type)
                && entry.Properties.Any(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)
                    && p.Metadata.Name is not ("UpdatedAt" or "UpdatedBy")),
            _ => false,
        };
    }
}
