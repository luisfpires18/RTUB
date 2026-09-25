namespace RTUB.Core.Enums.AfterHours;

/// <summary>The accepted action a receipt records. Stored as its integer value; do not renumber.</summary>
public enum PlayerActionKind
{
    Crime = 1,
    CoverJob = 2,
    Deposit = 3,
    Withdraw = 4,
    FenceSale = 5,
    ContractDelivery = 6,
    TrainSkill = 7,
    PurchaseGear = 8,
    EquipGear = 9,
    UnequipGear = 10,
    PvpAttack = 11,
    SaveDefence = 12,
    CreateFamily = 13,
    InviteToFamily = 14,
    CancelFamilyInvitation = 15,
    AcceptFamilyInvitation = 16,
    DeclineFamilyInvitation = 17,
    LeaveFamily = 18,
    TransferFamilyBoss = 19,
    SetFamilyRole = 20,
    UpdateFamilyProfile = 21,
    DonateToFamily = 22
}
