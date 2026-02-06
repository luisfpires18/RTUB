namespace RTUB.Core.Enums;

/// <summary>
/// Represents the biome/region in Stage Mode
/// Each region spans 100 stages
/// </summary>
public enum RegionType
{
    /// <summary>
    /// Stages 1-100: Forest region
    /// </summary>
    Forest = 0,

    /// <summary>
    /// Stages 101-200: Swamp region
    /// </summary>
    Swamp = 1,

    /// <summary>
    /// Stages 201-300: Mountains region
    /// </summary>
    Mountains = 2,

    /// <summary>
    /// Stages 301-400: Snowy region
    /// </summary>
    Snowy = 3,

    /// <summary>
    /// Stages 401-500: Ruins region
    /// </summary>
    Ruins = 4,

    /// <summary>
    /// Stages 501-600: Tropical region
    /// </summary>
    Tropical = 5,

    /// <summary>
    /// Stages 601-700: Caverns region
    /// </summary>
    Caverns = 6,

    /// <summary>
    /// Stages 701-800: Desert region
    /// </summary>
    Desert = 7,

    /// <summary>
    /// Stages 801-900: Volcanic region
    /// </summary>
    Volcanic = 8,

    /// <summary>
    /// Stages 901-1000: Dark region
    /// </summary>
    Dark = 9,

    /// <summary>
    /// Stages 10001+: Infinite Land (Endless mode)
    /// No checkpoints
    /// </summary>
    InfiniteLand = 99
}
