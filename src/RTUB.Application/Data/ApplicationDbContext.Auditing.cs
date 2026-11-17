using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Services;
using RTUB.Core.Constants;
using RTUB.Core.Entities;

namespace RTUB.Application.Data;

// Audit switches: which entities are audited, and temporarily turning auditing off.

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

    /// <summary>
    /// Temporarily disables audit logging for operations that need to save multiple times
    /// but should only create a single audit log entry.
    /// Use with caution and always re-enable auditing afterwards.
    /// </summary>
    public void DisableAuditing()
    {
        _isAuditingEnabled = false;
    }

    /// <summary>
    /// Re-enables audit logging after it was temporarily disabled.
    /// </summary>
    public void EnableAuditing()
    {
        _isAuditingEnabled = true;
    }
}
