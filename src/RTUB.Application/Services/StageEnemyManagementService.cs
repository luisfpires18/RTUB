using Microsoft.Extensions.Logging;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for Stage Enemy management (owner CRUD operations)
/// </summary>
public class StageEnemyManagementService : IStageEnemyManagementService
{
    private readonly IStageEnemyRepository _repository;
    private readonly ILogger<StageEnemyManagementService> _logger;

    public StageEnemyManagementService(
        IStageEnemyRepository repository,
        ILogger<StageEnemyManagementService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<StageEnemy>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync();
    }

    /// <inheritdoc />
    public async Task<StageEnemy?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(id);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<StageEnemy>> GetByRegionAsync(RegionType region, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByRegionAsync(region);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<StageEnemy>> GetByTypeAsync(EnemyType type, CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync();
        return all.Where(e => e.Type == type);
    }

    /// <inheritdoc />
    public async Task<StageEnemy> CreateAsync(
        string name,
        EnemyType type,
        RegionType region,
        int baseHP,
        int basePower,
        int baseSpeed,
        int baseDefense,
        double baseCriticalChance,
        decimal baseFidelisDrop,
        double finoDropChance,
        double shotDropChance,
        string? spritePath,
        int? bossStageNumber,
        PlacementType placement,
        CancellationToken cancellationToken = default)
    {
        var enemy = StageEnemy.Create(
            name, type, region,
            baseHP, basePower, baseSpeed, baseDefense,
            baseCriticalChance, baseFidelisDrop,
            finoDropChance, shotDropChance,
            spritePath, bossStageNumber, placement);

        var created = await _repository.AddAsync(enemy);
        _logger.LogInformation("Created stage enemy '{Name}' (Id={Id}, Type={Type}, Region={Region})",
            created.Name, created.Id, created.Type, created.Region);

        return created;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        int id,
        string name,
        EnemyType type,
        RegionType region,
        int baseHP,
        int basePower,
        int baseSpeed,
        int baseDefense,
        double baseCriticalChance,
        decimal baseFidelisDrop,
        double finoDropChance,
        double shotDropChance,
        string? spritePath,
        int? bossStageNumber,
        PlacementType placement,
        CancellationToken cancellationToken = default)
    {
        var enemy = await _repository.GetByIdOrThrowAsync(id);

        enemy.Name = name;
        enemy.Type = type;
        enemy.Region = region;
        enemy.BaseHP = baseHP;
        enemy.BasePower = basePower;
        enemy.BaseSpeed = baseSpeed;
        enemy.BaseDefense = baseDefense;
        enemy.BaseCriticalChance = baseCriticalChance;
        enemy.BaseFidelisDrop = baseFidelisDrop;
        enemy.FinoDropChance = finoDropChance;
        enemy.ShotDropChance = shotDropChance;
        enemy.SpritePath = spritePath;
        enemy.BossStageNumber = bossStageNumber;
        enemy.Placement = placement;
        enemy.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(enemy);
        _logger.LogInformation("Updated stage enemy '{Name}' (Id={Id})", enemy.Name, enemy.Id);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var enemy = await _repository.GetByIdOrThrowAsync(id);
        await _repository.DeleteAsync(enemy);
        _logger.LogInformation("Deleted stage enemy '{Name}' (Id={Id})", enemy.Name, enemy.Id);
    }
}
