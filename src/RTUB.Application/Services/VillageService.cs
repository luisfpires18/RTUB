using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service implementation for Village game operations.
/// All mutating methods tick resources first, then apply changes.
/// </summary>
public class VillageService : IVillageService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<VillageService> _logger;

    public VillageService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<VillageService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    // ======================== Get / Create ========================

    public async Task<VillageStateDto> GetOrCreateVillageStateAsync(string userId, CancellationToken ct = default)
    {
        using var context = _contextFactory.CreateDbContext();

        var village = await LoadVillageTracked(context, userId, ct);
        if (village == null)
        {
            village = Village.Create(userId);
            context.Villages.Add(village);
            await context.SaveChangesAsync(ct);

            // Initialize fields (4+4+4 = 12)
            foreach (var fieldDef in VillageFieldsConfig.All)
            {
                for (int i = 0; i < fieldDef.Count; i++)
                {
                    var field = VillageField.Create(village.Id, fieldDef.Resource, i);
                    context.VillageFields.Add(field);
                }
            }

            // Initialize troop records (one per unit type, all count=0)
            foreach (VillageUnitType unitType in Enum.GetValues<VillageUnitType>())
            {
                context.VillageTroops.Add(VillageTroop.Create(village.Id, unitType, 0));
            }

            await context.SaveChangesAsync(ct);

            // Reload with all children
            village = await LoadVillageTracked(context, userId, ct);
        }

        TickVillage(village!);
        CheckCompletedBuildJob(context, village!);
        await context.SaveChangesAsync(ct);

        return MapToDto(village!);
    }

    public async Task<VillageStateDto> TickAndGetStateAsync(string userId, CancellationToken ct = default)
    {
        using var context = _contextFactory.CreateDbContext();
        var village = await LoadVillageTracked(context, userId, ct);
        if (village == null)
            return await GetOrCreateVillageStateAsync(userId, ct);

        TickVillage(village);
        CheckCompletedBuildJob(context, village);
        CheckCompletedMissions(village);
        await context.SaveChangesAsync(ct);

        return MapToDto(village);
    }

    // ======================== Field Upgrade ========================

    public async Task<(bool Success, string Message, VillageStateDto? State)> UpgradeFieldAsync(
        string userId, VillageResourceType resourceType, int slotIndex, CancellationToken ct = default)
    {
        using var context = _contextFactory.CreateDbContext();
        var village = await LoadVillageTracked(context, userId, ct);
        if (village == null) return (false, "Village not found.", null);

        TickVillage(village);
        CheckCompletedBuildJob(context, village);

        // Check no active build job
        if (village.ActiveBuildJob != null && village.ActiveBuildJob.Status == VillageBuildJobStatus.Active && !village.ActiveBuildJob.IsFinished)
            return (false, "A construction is already in progress.", MapToDto(village));

        // Find the field
        var field = village.Fields.FirstOrDefault(f => f.ResourceType == resourceType && f.SlotIndex == slotIndex);
        if (field == null) return (false, "Field not found.", MapToDto(village));

        var toLevel = field.Level + 1;
        var (costW, costS, costF) = VillageGameRules.GetFieldUpgradeCost(resourceType, toLevel);

        if (!village.CanAfford(costW, costS, costF))
            return (false, "Not enough resources.", MapToDto(village));

        village.SpendResources(costW, costS, costF);

        var durationSec = VillageGameRules.GetFieldUpgradeTime(resourceType, toLevel);
        var job = VillageBuildJob.CreateFieldJob(village.Id, resourceType, slotIndex, toLevel, durationSec);
        context.VillageBuildJobs.Add(job);
        village.ActiveBuildJob = job;

        await context.SaveChangesAsync(ct);
        return (true, $"Upgrading {VillageFieldsConfig.GetByResource(resourceType).Name} #{slotIndex + 1} to Lv {toLevel}.", MapToDto(village));
    }

    // ======================== Building Construct / Upgrade ========================

    public async Task<(bool Success, string Message, VillageStateDto? State)> ConstructBuildingAsync(
        string userId, VillageBuildingType buildingType, CancellationToken ct = default)
    {
        using var context = _contextFactory.CreateDbContext();
        var village = await LoadVillageTracked(context, userId, ct);
        if (village == null) return (false, "Village not found.", null);

        TickVillage(village);
        CheckCompletedBuildJob(context, village);

        if (village.ActiveBuildJob != null && village.ActiveBuildJob.Status == VillageBuildJobStatus.Active && !village.ActiveBuildJob.IsFinished)
            return (false, "A construction is already in progress.", MapToDto(village));

        // Check not already built
        if (village.Buildings.Any(b => b.BuildingType == buildingType))
            return (false, "Building already exists. Use upgrade instead.", MapToDto(village));

        // Check prerequisites
        var cityLevels = GetCityLevels(village);
        if (!VillageGameRules.CanBuild(buildingType, cityLevels))
        {
            var unmet = VillageGameRules.GetUnmetRequirements(buildingType, cityLevels);
            return (false, $"Prerequisites not met: {string.Join(", ", unmet)}", MapToDto(village));
        }

        var toLevel = 1;
        var (costW, costS, costF) = VillageGameRules.GetBuildingCost(buildingType, toLevel);

        if (!village.CanAfford(costW, costS, costF))
            return (false, "Not enough resources.", MapToDto(village));

        village.SpendResources(costW, costS, costF);

        // Create the building record at level 0 (will be set to 1 on job completion)
        var building = VillageBuilding.Create(village.Id, buildingType, 0);
        context.VillageBuildings.Add(building);

        var durationSec = VillageGameRules.GetBuildingTime(buildingType, toLevel);
        var job = VillageBuildJob.CreateBuildingJob(village.Id, buildingType, toLevel, durationSec);
        context.VillageBuildJobs.Add(job);
        village.ActiveBuildJob = job;

        await context.SaveChangesAsync(ct);
        return (true, $"Constructing {VillageBuildingsConfig.DisplayName(buildingType)} to Lv {toLevel}.", MapToDto(village));
    }

    public async Task<(bool Success, string Message, VillageStateDto? State)> UpgradeBuildingAsync(
        string userId, VillageBuildingType buildingType, CancellationToken ct = default)
    {
        using var context = _contextFactory.CreateDbContext();
        var village = await LoadVillageTracked(context, userId, ct);
        if (village == null) return (false, "Village not found.", null);

        TickVillage(village);
        CheckCompletedBuildJob(context, village);

        if (village.ActiveBuildJob != null && village.ActiveBuildJob.Status == VillageBuildJobStatus.Active && !village.ActiveBuildJob.IsFinished)
            return (false, "A construction is already in progress.", MapToDto(village));

        var existing = village.Buildings.FirstOrDefault(b => b.BuildingType == buildingType);
        if (existing == null || existing.Level < 1)
            return (false, "Building not found. Construct it first.", MapToDto(village));

        var toLevel = existing.Level + 1;
        var (costW, costS, costF) = VillageGameRules.GetBuildingCost(buildingType, toLevel);

        if (!village.CanAfford(costW, costS, costF))
            return (false, "Not enough resources.", MapToDto(village));

        village.SpendResources(costW, costS, costF);

        var durationSec = VillageGameRules.GetBuildingTime(buildingType, toLevel);
        var job = VillageBuildJob.CreateBuildingJob(village.Id, buildingType, toLevel, durationSec);
        context.VillageBuildJobs.Add(job);
        village.ActiveBuildJob = job;

        await context.SaveChangesAsync(ct);
        return (true, $"Upgrading {VillageBuildingsConfig.DisplayName(buildingType)} to Lv {toLevel}.", MapToDto(village));
    }

    // ======================== Train Troops ========================

    public async Task<(bool Success, string Message, VillageStateDto? State)> TrainTroopsAsync(
        string userId, VillageUnitType unitType, int count, CancellationToken ct = default)
    {
        if (count <= 0) return (false, "Count must be positive.", null);

        using var context = _contextFactory.CreateDbContext();
        var village = await LoadVillageTracked(context, userId, ct);
        if (village == null) return (false, "Village not found.", null);

        TickVillage(village);

        var unitDef = VillageUnitsConfig.Get(unitType);

        // Check if the required building exists
        var requiredBuilding = unitDef.Building switch
        {
            "CentroAcademico" => VillageBuildingType.CentroAcademico,
            _ => VillageBuildingType.CentroAcademico
        };

        var building = village.Buildings.FirstOrDefault(b => b.BuildingType == requiredBuilding);
        if (building == null || building.Level < 1)
            return (false, $"You need a {unitDef.Building} to train {unitDef.Name}.", MapToDto(village));

        // Check resources for total
        var totalCostW = unitDef.CostWood * count;
        var totalCostS = unitDef.CostStone * count;
        var totalCostF = unitDef.CostFood * count;

        if (!village.CanAfford(totalCostW, totalCostS, totalCostF))
            return (false, "Not enough resources.", MapToDto(village));

        village.SpendResources(totalCostW, totalCostS, totalCostF);

        // Add troops instantly (simplification — no training queue for now)
        var troopRecord = village.Troops.First(t => t.UnitType == unitType);
        troopRecord.Count += count;

        await context.SaveChangesAsync(ct);
        return (true, $"Trained {count}x {unitDef.Name}.", MapToDto(village));
    }

    // ======================== Missions ========================

    public async Task<(bool Success, string Message, VillageStateDto? State)> DispatchMissionAsync(
        string userId, string missionKey, VillageMissionDifficulty difficulty,
        Dictionary<VillageUnitType, int> troopAllocation, CancellationToken ct = default)
    {
        using var context = _contextFactory.CreateDbContext();
        var village = await LoadVillageTracked(context, userId, ct);
        if (village == null) return (false, "Village not found.", null);

        TickVillage(village);

        var template = VillageMissionsConfig.GetByKey(missionKey);
        if (template == null) return (false, "Unknown mission.", MapToDto(village));

        // Validate troops
        var totalSent = troopAllocation.Values.Sum();
        if (totalSent <= 0)
            return (false, "You must send at least one troop.", MapToDto(village));

        foreach (var kvp in troopAllocation)
        {
            if (kvp.Value <= 0) continue;
            var troopRecord = village.Troops.FirstOrDefault(t => t.UnitType == kvp.Key);
            if (troopRecord == null || troopRecord.Count < kvp.Value)
                return (false, $"Not enough {VillageUnitsConfig.Get(kvp.Key).Name}.", MapToDto(village));
        }

        // Check power requirement
        var derived = VillageGameRules.GetMissionDerived(template, difficulty);
        var totalAttack = VillageGameRules.TotalAttackPower(troopAllocation);
        if (totalAttack < derived.RequiredPower)
            return (false, $"Insufficient power ({totalAttack}/{derived.RequiredPower}).", MapToDto(village));

        // Deduct troops
        foreach (var kvp in troopAllocation)
        {
            if (kvp.Value <= 0) continue;
            var troopRecord = village.Troops.First(t => t.UnitType == kvp.Key);
            troopRecord.Count -= kvp.Value;
        }

        // Create mission
        var sentJson = JsonSerializer.Serialize(
            troopAllocation.Where(kvp => kvp.Value > 0).ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));

        var mission = VillageMission.Create(
            village.Id, missionKey, template.Name, difficulty, sentJson, derived.DurationSec);
        context.VillageMissions.Add(mission);

        await context.SaveChangesAsync(ct);
        return (true, $"Mission '{template.Name}' dispatched! Returns in {derived.DurationSec}s.", MapToDto(village));
    }

    public async Task<(bool Success, string Message, VillageStateDto? State)> ClaimMissionAsync(
        string userId, int missionId, CancellationToken ct = default)
    {
        using var context = _contextFactory.CreateDbContext();
        var village = await LoadVillageTracked(context, userId, ct);
        if (village == null) return (false, "Village not found.", null);

        TickVillage(village);
        CheckCompletedMissions(village);

        var mission = village.Missions.FirstOrDefault(m => m.Id == missionId);
        if (mission == null) return (false, "Mission not found.", MapToDto(village));
        if (mission.Status == VillageMissionStatus.Ongoing)
            return (false, "Mission not yet complete.", MapToDto(village));
        if (mission.Claimed)
            return (false, "Already claimed.", MapToDto(village));

        mission.Claimed = true;

        if (mission.Status == VillageMissionStatus.Success && mission.RewardJson != null)
        {
            var reward = JsonSerializer.Deserialize<Dictionary<string, double>>(mission.RewardJson);
            if (reward != null)
            {
                var storageCap = GetStorageCap(village);
                village.AddResources(
                    reward.GetValueOrDefault("Wood"),
                    reward.GetValueOrDefault("Stone"),
                    reward.GetValueOrDefault("Food"),
                    storageCap);
            }
        }

        // Return surviving troops
        var sentTroops = JsonSerializer.Deserialize<Dictionary<string, int>>(mission.SentTroopsJson) ?? new();
        var losses = mission.LossesJson != null
            ? JsonSerializer.Deserialize<Dictionary<string, int>>(mission.LossesJson) ?? new()
            : new Dictionary<string, int>();

        foreach (var kvp in sentTroops)
        {
            if (Enum.TryParse<VillageUnitType>(kvp.Key, out var unitType))
            {
                var lostCount = losses.GetValueOrDefault(kvp.Key);
                var surviving = Math.Max(0, kvp.Value - lostCount);
                if (surviving > 0)
                {
                    var troopRecord = village.Troops.FirstOrDefault(t => t.UnitType == unitType);
                    if (troopRecord != null)
                        troopRecord.Count += surviving;
                }
            }
        }

        await context.SaveChangesAsync(ct);
        return (true, mission.Status == VillageMissionStatus.Success ? "Rewards claimed!" : "Mission results acknowledged.", MapToDto(village));
    }

    public async Task<(bool Success, string Message, VillageStateDto? State)> DismissMissionAsync(
        string userId, int missionId, CancellationToken ct = default)
    {
        using var context = _contextFactory.CreateDbContext();
        var village = await LoadVillageTracked(context, userId, ct);
        if (village == null) return (false, "Village not found.", null);

        var mission = village.Missions.FirstOrDefault(m => m.Id == missionId);
        if (mission == null) return (false, "Mission not found.", MapToDto(village));
        if (!mission.Claimed && mission.Status != VillageMissionStatus.Ongoing)
        {
            // Auto-claim returning troops for dismissed missions
            mission.Claimed = true;
        }

        context.VillageMissions.Remove(mission);
        await context.SaveChangesAsync(ct);
        return (true, "Mission dismissed.", MapToDto(village));
    }

    // ======================== Helpers ========================

    private async Task<Village?> LoadVillageTracked(ApplicationDbContext context, string userId, CancellationToken ct)
    {
        return await context.Villages
            .Include(v => v.Fields)
            .Include(v => v.Buildings)
            .Include(v => v.Troops)
            .Include(v => v.Missions)
            .Include(v => v.ActiveBuildJob)
            .FirstOrDefaultAsync(v => v.UserId == userId, ct);
    }

    private void TickVillage(Village village)
    {
        var storageCap = GetStorageCap(village);
        var (woodRate, stoneRate, foodRate) = GetProductionRates(village);
        village.TickResources(woodRate, stoneRate, foodRate, storageCap);
    }

    private void CheckCompletedBuildJob(ApplicationDbContext context, Village village)
    {
        var job = village.ActiveBuildJob;
        if (job == null || job.Status != VillageBuildJobStatus.Active) return;
        if (!job.IsFinished) return;

        job.Status = VillageBuildJobStatus.Complete;

        if (job.IsFieldUpgrade && job.FieldResourceType.HasValue && job.FieldSlotIndex.HasValue)
        {
            var field = village.Fields.FirstOrDefault(f =>
                f.ResourceType == job.FieldResourceType.Value && f.SlotIndex == job.FieldSlotIndex.Value);
            if (field != null)
                field.Level = job.ToLevel;
        }
        else if (!job.IsFieldUpgrade && job.BuildingType.HasValue)
        {
            var building = village.Buildings.FirstOrDefault(b => b.BuildingType == job.BuildingType.Value);
            if (building != null)
                building.Level = job.ToLevel;
        }

        // Clear the active job reference
        village.ActiveBuildJob = null;
    }

    private void CheckCompletedMissions(Village village)
    {
        foreach (var mission in village.Missions.Where(m => m.Status == VillageMissionStatus.Ongoing && m.IsFinished))
        {
            var template = VillageMissionsConfig.GetByKey(mission.MissionKey);
            if (template == null) continue;

            var derived = VillageGameRules.GetMissionDerived(template, mission.Difficulty);
            var sentTroops = JsonSerializer.Deserialize<Dictionary<string, int>>(mission.SentTroopsJson) ?? new();

            // Convert to VillageUnitType dictionary for calculation
            var troopDict = new Dictionary<VillageUnitType, int>();
            foreach (var kvp in sentTroops)
            {
                if (Enum.TryParse<VillageUnitType>(kvp.Key, out var unitType))
                    troopDict[unitType] = kvp.Value;
            }

            var totalAttack = VillageGameRules.TotalAttackPower(troopDict);

            // Mission succeeds if attack power meets requirement
            if (totalAttack >= derived.RequiredPower)
            {
                mission.Status = VillageMissionStatus.Success;
                mission.RewardJson = JsonSerializer.Serialize(new Dictionary<string, double>
                {
                    ["Wood"] = derived.RewardWood,
                    ["Stone"] = derived.RewardStone,
                    ["Food"] = derived.RewardFood
                });
            }
            else
            {
                mission.Status = VillageMissionStatus.Failed;
            }

            // Calculate losses
            var losses = VillageGameRules.CalculateLosses(troopDict, totalAttack, derived.RequiredPower);
            if (losses.Count > 0)
            {
                mission.LossesJson = JsonSerializer.Serialize(
                    losses.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));
            }
        }
    }

    private double GetStorageCap(Village village)
    {
        var salaDaTunaLevel = village.Buildings
            .FirstOrDefault(b => b.BuildingType == VillageBuildingType.SalaDaTuna)?.Level ?? 0;
        return VillageGameRules.GetStorageCap(salaDaTunaLevel);
    }

    private (double WoodRate, double StoneRate, double FoodRate) GetProductionRates(Village village)
    {
        double woodRate = 0, stoneRate = 0, foodRate = 0;

        foreach (var field in village.Fields)
        {
            var fieldDef = VillageFieldsConfig.GetByResource(field.ResourceType);
            var rate = VillageGameRules.GetFieldProductionRate(fieldDef.BaseProd, field.Level);

            switch (field.ResourceType)
            {
                case VillageResourceType.Wood: woodRate += rate; break;
                case VillageResourceType.Stone: stoneRate += rate; break;
                case VillageResourceType.Food: foodRate += rate; break;
            }
        }

        return (woodRate, stoneRate, foodRate);
    }

    private Dictionary<VillageBuildingType, int> GetCityLevels(Village village)
    {
        return village.Buildings.ToDictionary(b => b.BuildingType, b => b.Level);
    }

    private VillageStateDto MapToDto(Village village)
    {
        var storageCap = GetStorageCap(village);
        var (woodRate, stoneRate, foodRate) = GetProductionRates(village);
        var cityLevels = GetCityLevels(village);

        // Population = sum of food field levels
        var population = village.Fields
            .Where(f => f.ResourceType == VillageResourceType.Food)
            .Sum(f => f.Level);

        var dto = new VillageStateDto
        {
            VillageId = village.Id,
            Kingdom = village.Kingdom,
            PosX = village.PosX,
            PosY = village.PosY,
            Wood = village.Wood,
            Stone = village.Stone,
            Food = village.Food,
            StorageCap = storageCap,
            WoodRate = woodRate,
            StoneRate = stoneRate,
            FoodRate = foodRate,
            Population = population,
            Prestige = 0,
            Money = 0,
            Buildings = cityLevels,
            Troops = village.Troops.ToDictionary(t => t.UnitType, t => t.Count),
        };

        // Fields
        foreach (var field in village.Fields.OrderBy(f => f.ResourceType).ThenBy(f => f.SlotIndex))
        {
            var toLevel = field.Level + 1;
            var (costW, costS, costF) = VillageGameRules.GetFieldUpgradeCost(field.ResourceType, toLevel);
            dto.Fields.Add(new FieldDto
            {
                ResourceType = field.ResourceType,
                SlotIndex = field.SlotIndex,
                Level = field.Level,
                NextCostWood = costW,
                NextCostStone = costS,
                NextCostFood = costF,
                NextUpgradeTimeSec = VillageGameRules.GetFieldUpgradeTime(field.ResourceType, toLevel)
            });
        }

        // Active build job
        if (village.ActiveBuildJob != null && village.ActiveBuildJob.Status == VillageBuildJobStatus.Active)
        {
            var job = village.ActiveBuildJob;
            string buildingName;
            if (job.IsFieldUpgrade && job.FieldResourceType.HasValue)
                buildingName = VillageFieldsConfig.GetByResource(job.FieldResourceType.Value).Name;
            else if (job.BuildingType.HasValue)
                buildingName = VillageBuildingsConfig.DisplayName(job.BuildingType.Value);
            else
                buildingName = "Unknown";

            dto.ActiveBuildJob = new BuildJobDto
            {
                Id = job.Id,
                IsFieldUpgrade = job.IsFieldUpgrade,
                BuildingName = buildingName,
                ToLevel = job.ToLevel,
                StartedAt = job.StartedAt,
                DurationSecs = job.DurationSecs,
                FinishesAt = job.FinishesAt,
                RemainingSeconds = job.RemainingSeconds,
                Progress = job.Progress
            };
        }

        // Missions
        foreach (var mission in village.Missions.OrderByDescending(m => m.StartedAt))
        {
            var mDto = new MissionDto
            {
                Id = mission.Id,
                MissionKey = mission.MissionKey,
                Name = mission.Name,
                Difficulty = mission.Difficulty.ToString(),
                Status = mission.Status.ToString(),
                SentTroops = JsonSerializer.Deserialize<Dictionary<string, int>>(mission.SentTroopsJson) ?? new(),
                StartedAt = mission.StartedAt,
                FinishesAt = mission.FinishesAt,
                RemainingSeconds = mission.RemainingSeconds,
                Claimed = mission.Claimed
            };

            if (mission.RewardJson != null)
            {
                var reward = JsonSerializer.Deserialize<Dictionary<string, double>>(mission.RewardJson);
                if (reward != null)
                {
                    mDto.RewardWood = reward.GetValueOrDefault("Wood");
                    mDto.RewardStone = reward.GetValueOrDefault("Stone");
                    mDto.RewardFood = reward.GetValueOrDefault("Food");
                }
            }

            if (mission.LossesJson != null)
                mDto.Losses = JsonSerializer.Deserialize<Dictionary<string, int>>(mission.LossesJson);

            if (mission.Status == VillageMissionStatus.Ongoing || !mission.Claimed)
                dto.ActiveMissions.Add(mDto);
            else
                dto.CompletedMissions.Add(mDto);
        }

        // Available buildings (not yet built, prerequisites met)
        foreach (var buildingType in Enum.GetValues<VillageBuildingType>())
        {
            if (cityLevels.ContainsKey(buildingType)) continue; // already built
            if (VillageGameRules.CanBuild(buildingType, cityLevels))
                dto.AvailableBuildings.Add(buildingType);
        }

        return dto;
    }
}
