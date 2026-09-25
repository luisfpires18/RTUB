namespace RTUB.Core.Enums.AfterHours;

/// <summary>What an objective counts. Derived server-side from accepted actions only.</summary>
public enum ObjectiveMetric
{
    SuccessfulCrimes = 1,
    CrimeCash = 2,
    CrimeEnergy = 3,
    EnergySpent = 4,
    CoverJobsReducingHeat = 5,
    CargoMoved = 6,
    FencedUnits = 7,
    ContractsDelivered = 8,
    PvpWins = 9,
    Donated = 10,
    SuccessfulCrimesAsFamilyMember = 11,
    QualifiedPvpWins = 12
}
