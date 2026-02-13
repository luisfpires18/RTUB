namespace RTUB.Core.Enums;

/// <summary>
/// Represents the biome/region in Stage Mode
/// 11 biomes × 100 stages each (1-1100), Void is 1101+ (endless)
/// </summary>
public enum RegionType
{
    /// <summary>
    /// Stages 1-100: Forest region
    /// </summary>
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

    /// <summary>Stages 901-1000: Dark region</summary>
    Dark = 9,

    /// <summary>Stages 1001-1100: Light region</summary>
    Light = 10,

    /// <summary>
    /// Stages 1101+: The Void (Endless mode)
    /// Enemies and bosses are drawn randomly across all void sub-folders
    /// </summary>
    Void = 99
}
