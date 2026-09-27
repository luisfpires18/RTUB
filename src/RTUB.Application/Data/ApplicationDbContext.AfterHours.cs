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
    public DbSet<BuyerContract> AfterHoursBuyerContracts { get; set; } = null!;
    public DbSet<BuyerContractCompletion> AfterHoursBuyerContractCompletions { get; set; } = null!;
}
