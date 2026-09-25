using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Data;

/// <summary>
/// After Hours DbSets. Mapping lives in Data/Configurations/AfterHours. See docs/after_hours/README.md.
/// </summary>
public partial class ApplicationDbContext
{
    public DbSet<GameCycle> AfterHoursGameCycles { get; set; } = null!;
    public DbSet<PlayerCycleState> AfterHoursPlayerCycleStates { get; set; } = null!;
    public DbSet<PlayerActionReceipt> AfterHoursPlayerActionReceipts { get; set; } = null!;
    public DbSet<PlayerCargo> AfterHoursPlayerCargo { get; set; } = null!;
    public DbSet<PlayerGear> AfterHoursPlayerGear { get; set; } = null!;
    public DbSet<PvpBattle> AfterHoursPvpBattles { get; set; } = null!;
    public DbSet<Family> AfterHoursFamilies { get; set; } = null!;
    public DbSet<FamilyMembership> AfterHoursFamilyMemberships { get; set; } = null!;
    public DbSet<FamilyInvitation> AfterHoursFamilyInvitations { get; set; } = null!;
    public DbSet<FamilyCycleState> AfterHoursFamilyCycleStates { get; set; } = null!;
    public DbSet<PlayerObjectiveProgress> AfterHoursPlayerObjectiveProgress { get; set; } = null!;
    public DbSet<FamilyObjectiveProgress> AfterHoursFamilyObjectiveProgress { get; set; } = null!;
    public DbSet<PvpObjectiveCredit> AfterHoursPvpObjectiveCredits { get; set; } = null!;
    public DbSet<BuyerContract> AfterHoursBuyerContracts { get; set; } = null!;
    public DbSet<BuyerContractCompletion> AfterHoursBuyerContractCompletions { get; set; } = null!;
    public DbSet<CycleArchive> AfterHoursCycleArchives { get; set; } = null!;
    public DbSet<YearbookPlayerEntry> AfterHoursYearbookPlayers { get; set; } = null!;
    public DbSet<YearbookFamilyEntry> AfterHoursYearbookFamilies { get; set; } = null!;
    public DbSet<YearbookFamilyMember> AfterHoursYearbookFamilyMembers { get; set; } = null!;
    public DbSet<AfterHoursTuningSetting> AfterHoursTuningSettings { get; set; } = null!;
    public DbSet<AfterHoursCosmeticAward> AfterHoursCosmeticAwards { get; set; } = null!;
}
