namespace RTUB.Core.Entities.AfterHours;

/// <summary>
/// An Owner override of one <see cref="Helpers.AfterHours.AfterHoursTuning"/> value (AH-010). One row per key
/// (unique index); no row means the default. Stored as invariant text and validated on write and on read, so a
/// hand-edited bad value falls back to the default instead of breaking play. Audited like other admin records.
/// </summary>
public class AfterHoursTuningSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedByUserId { get; set; }
}

/// <summary>
/// A cosmetic title granted by an Owner (AH-010). No gameplay effect, not tied to a cycle's power, so it
/// survives rollovers. <see cref="UserId"/> is a historical value, not a foreign key, like the yearbook:
/// deleting the account leaves the record. Revoking sets <see cref="RevokedAtUtc"/>; rows are never deleted.
/// </summary>
public class AfterHoursCosmeticAward : BaseEntity
{
    public const int TitleMinLength = 2;
    public const int TitleMaxLength = 40;
    public const int DescriptionMaxLength = 160;

    public string UserId { get; set; } = string.Empty;

    /// <summary>The recipient's display name when granted, so the record reads without the account.</summary>
    public string RecipientName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime GrantedAtUtc { get; set; }
    public string GrantedByUserId { get; set; } = string.Empty;
    public int? FiscalYearId { get; set; }
    public int? CycleArchiveId { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedByUserId { get; set; }

    public bool IsActive => RevokedAtUtc is null;
}
