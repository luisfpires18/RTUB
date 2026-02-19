namespace RTUB.Core.Enums;

/// <summary>
/// Represents the biome/region in Stage Mode.
/// 20 biomes × 1000 floors each (1-20000), Arena is 20001+ (endless).
/// </summary>
public enum RegionType
{
    /// <summary>Stages 1-100: Forest region</summary>
    Forest = 0,

    /// <summary>Stages 101-200: Swamp region</summary>
    Swamp = 1,

    /// <summary>Stages 201-300: Mountains region</summary>
    Mountains = 2,

    /// <summary>Stages 301-400: Snowy region</summary>
    Snowy = 3,

    /// <summary>Stages 401-500: Tropical region</summary>
    Tropical = 4,

    /// <summary>Stages 501-600: Caverns region</summary>
    Caverns = 5,

    /// <summary>Stages 601-700: Desert region</summary>
    Desert = 6,

    /// <summary>Stages 701-800: Volcanic region</summary>
    Volcanic = 7,

    /// <summary>Stages 801-900: Ruins region</summary>
    Ruins = 8,

    /// <summary>Stages 901-1000: Sky region</summary>
    Sky = 9,

    /// <summary>Stages 1001-1100: Underwater region</summary>
    Underwater = 10,

    /// <summary>Stages 1101-1200: Underground region</summary>
    Underground = 11,

    /// <summary>Stages 1201-1300: Mechanical region</summary>
    Mechanical = 12,

    /// <summary>Stages 1301-1400: Frostfire region</summary>
    Frostfire = 13,

    /// <summary>Stages 1401-1500: Corruption region</summary>
    Corruption = 14,

    /// <summary>Stages 1501-1600: Dark region</summary>
    Dark = 15,

    /// <summary>Stages 1601-1700: Alien region</summary>
    Alien = 16,

    /// <summary>Stages 1701-1800: Void region</summary>
    Void = 17,

    /// <summary>Stages 1801-1900: Timerift region</summary>
    Timerift = 18,

    /// <summary>Stages 1901-2000: Light region</summary>
    Light = 19,

    /// <summary>
    /// Floors 20001+: The Arena (Endless mode).
    /// All enemies and bosses are drawn randomly from previous stages.
    /// </summary>
    Arena = 20
}
