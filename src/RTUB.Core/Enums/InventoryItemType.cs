namespace RTUB.Core.Enums;

/// <summary>
/// Represents the type of inventory item
/// </summary>
public enum InventoryItemType
{
    /// <summary>
    /// Beer item - used for healing characters
    /// </summary>
    Beer = 1,

    /// <summary>
    /// Shot item - dropped by stage enemies, used for special abilities
    /// </summary>
    Shot = 2,

    /// <summary>
    /// Vodka - gathered resource, used for crafting (costs 1 energy)
    /// </summary>
    Vodka = 3,

    /// <summary>
    /// Gin - gathered resource, used for crafting (costs 2 energy)
    /// </summary>
    Gin = 4,

    /// <summary>
    /// Whisky - gathered resource, used for crafting (costs 3 energy)
    /// </summary>
    Whisky = 5,

    /// <summary>
    /// Absinto - gathered resource, used for crafting (costs 4 energy)
    /// </summary>
    Absinto = 6,

    // ── Instrument Parts (dropped in Stage Mode, used for future crafting) ──

    /// <summary>Guitarra part - rare stage drop for crafting</summary>
    GuitarraPart = 100,
    /// <summary>Bandolim part - rare stage drop for crafting</summary>
    BandolimPart = 101,
    /// <summary>Cavaquinho part - rare stage drop for crafting</summary>
    CavaquinhoPart = 102,
    /// <summary>Acordeão part - rare stage drop for crafting</summary>
    AcordeaoPart = 103,
    /// <summary>Fagote part - rare stage drop for crafting</summary>
    FagotePart = 104,
    /// <summary>Flauta part - rare stage drop for crafting</summary>
    FlautaPart = 105,
    /// <summary>Baixo part - rare stage drop for crafting</summary>
    BaixoPart = 106,
    /// <summary>Contrabaixo part - rare stage drop for crafting</summary>
    ContrabaixoPart = 107,
    /// <summary>Percussão part - rare stage drop for crafting</summary>
    PercussaoPart = 108,
    /// <summary>Pandeireta part - rare stage drop for crafting</summary>
    PandeiretaPart = 109,
    /// <summary>Estandarte part - rare stage drop for crafting</summary>
    EstandartePart = 110,
    /// <summary>Violino part - rare stage drop for crafting</summary>
    ViolinoPart = 111,
    /// <summary>Saxofone part - rare stage drop for crafting</summary>
    SaxofonePart = 112,

    // ── Equipment Pieces (dropped in Stage Mode, used for future crafting) ──

    /// <summary>Head equipment piece - stage drop for crafting</summary>
    EquipmentHead = 200,
    /// <summary>Shoulders equipment piece - stage drop for crafting</summary>
    EquipmentShoulders = 201,
    /// <summary>Chest equipment piece - stage drop for crafting</summary>
    EquipmentChest = 202,
    /// <summary>Gloves equipment piece - stage drop for crafting</summary>
    EquipmentGloves = 203,
    /// <summary>Legs equipment piece - stage drop for crafting</summary>
    EquipmentLegs = 204,
    /// <summary>Boots equipment piece - stage drop for crafting</summary>
    EquipmentBoots = 205
}
