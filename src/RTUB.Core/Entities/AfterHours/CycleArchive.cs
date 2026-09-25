using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Entities.AfterHours;

/// <summary>
/// The yearbook of one finished cycle: written once, in the same transaction that finishes the cycle,
/// and never changed afterwards. Everything the yearbook shows is a snapshot taken at that moment, so
/// later renames, family moves or rule changes never alter it. One archive per cycle (unique index).
/// </summary>
public class CycleArchive : BaseEntity
{
    public int GameCycleId { get; set; }
    public GameCycleKind Kind { get; set; }
    public int FiscalYearId { get; set; }

    /// <summary>Snapshot of the fiscal year's label, e.g. "2026-2027".</summary>
    public string FiscalYearLabel { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public DateTime ArchivedAtUtc { get; set; }

    /// <summary>Live archives are official; Pilot archives never are, and never have champions.</summary>
    public bool Official { get; set; }

    /// <summary>The cycle the rollover started. Set in the same transaction; unique.</summary>
    public int? NextGameCycleId { get; set; }

    public List<YearbookPlayerEntry> Players { get; set; } = [];
    public List<YearbookFamilyEntry> Families { get; set; } = [];
}

/// <summary>A player's final standing in an archived cycle. No wallet, bank, cargo or battle details.</summary>
public class YearbookPlayerEntry : BaseEntity
{
    public int CycleArchiveId { get; set; }

    /// <summary>Historical value, not a foreign key: the entry survives deletion of the account.</summary>
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Level { get; set; }
    public long XP { get; set; }
    public int Toughness { get; set; }
    public int Stealth { get; set; }
    public int Smarts { get; set; }
    public int Charisma { get; set; }
    public int AnnualScore { get; set; }

    /// <summary>Competition rank: equal scores share a rank (1, 1, 3).</summary>
    public int Rank { get; set; }

    public int ScoringWeeks { get; set; }

    /// <summary>The player's family at the end of the cycle, if any.</summary>
    public int? FamilyId { get; set; }
    public string? FamilyName { get; set; }

    public bool IsChampion { get; set; }
}

/// <summary>A family's final standing in an archived cycle, with its roster at the end of that cycle.</summary>
public class YearbookFamilyEntry : BaseEntity
{
    public int CycleArchiveId { get; set; }
    public int FamilyId { get; set; }
    public string FamilyName { get; set; } = string.Empty;
    public int AnnualScore { get; set; }
    public int Rank { get; set; }
    public int ScoringWeeks { get; set; }
    public bool IsChampion { get; set; }
    public List<YearbookFamilyMember> Members { get; set; } = [];
}

/// <summary>One member of a family's end-of-cycle roster.</summary>
public class YearbookFamilyMember : BaseEntity
{
    public int YearbookFamilyEntryId { get; set; }

    /// <summary>Historical value, not a foreign key: the roster entry survives deletion of the account.</summary>
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public FamilyRole Role { get; set; }
}
