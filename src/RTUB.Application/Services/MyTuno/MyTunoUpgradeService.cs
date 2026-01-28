using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services.MyTuno;

public class MyTunoUpgradeService
{
    private const int BaseCost = 10;
    private const int IncrementCost = 5;

    private readonly ApplicationDbContext _context;
    private readonly MyTunoCharacterService _characterService;

    public MyTunoUpgradeService(ApplicationDbContext context, MyTunoCharacterService characterService)
    {
        _context = context;
        _characterService = characterService;
    }

    public decimal GetUpgradeCost(MyTunoStatType statType, Character character)
    {
        var upgrades = statType switch
        {
            MyTunoStatType.Hp => character.HpUpgrades,
            MyTunoStatType.Power => character.PowerUpgrades,
            MyTunoStatType.Speed => character.SpeedUpgrades,
            _ => throw new ArgumentOutOfRangeException(nameof(statType), statType, null)
        };

        return BaseCost + upgrades * IncrementCost;
    }

    public async Task<MyTunoUpgradeResult> UpgradeAsync(string userId, MyTunoStatType statType)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var character = await _context.Characters.FirstOrDefaultAsync(c => c.UserId == userId)
                        ?? await _characterService.GetOrCreateAsync(userId);

        var cost = GetUpgradeCost(statType, character);
        if (user.FidelisBalance < cost)
        {
            throw new InvalidOperationException("Saldo de Fidelis insuficiente.");
        }

        user.FidelisBalance -= cost;

        switch (statType)
        {
            case MyTunoStatType.Hp:
                character.Hp += 2;
                character.HpUpgrades += 1;
                break;
            case MyTunoStatType.Power:
                character.Power += 1;
                character.PowerUpgrades += 1;
                break;
            case MyTunoStatType.Speed:
                character.Speed += 1;
                character.SpeedUpgrades += 1;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(statType), statType, null);
        }

        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("A atualização falhou por concorrência. Tente novamente.");
        }

        return new MyTunoUpgradeResult(
            character.Hp,
            character.Power,
            character.Speed,
            character.HpUpgrades,
            character.PowerUpgrades,
            character.SpeedUpgrades,
            user.FidelisBalance);
    }
}
