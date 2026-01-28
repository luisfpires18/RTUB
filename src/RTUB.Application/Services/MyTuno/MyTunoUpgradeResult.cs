namespace RTUB.Application.Services.MyTuno;

public record MyTunoUpgradeResult(
    int NewHp,
    int NewPower,
    int NewSpeed,
    int HpUpgrades,
    int PowerUpgrades,
    int SpeedUpgrades,
    decimal FidelisBalance);
