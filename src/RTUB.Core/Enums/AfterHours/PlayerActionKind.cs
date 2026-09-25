namespace RTUB.Core.Enums.AfterHours;

/// <summary>The accepted action a receipt records. Stored as its integer value; do not renumber.</summary>
public enum PlayerActionKind
{
    Crime = 1,
    CoverJob = 2,
    Deposit = 3,
    Withdraw = 4,
    FenceSale = 5,
    ContractDelivery = 6
}
