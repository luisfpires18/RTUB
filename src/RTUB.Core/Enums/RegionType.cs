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
    /// Stages 101-200: Desert region
    /// </summary>
    Desert = 1,

    /// <summary>
    /// Stages 201-300: Mountains region
    /// </summary>
    Mountains = 2,

    /// <summary>
    /// Stages 301-400: Swamp region
    /// </summary>
    Swamp = 3,

    /// <summary>
    /// Stages 401-500: Tundra region
    /// </summary>
    Tundra = 4,

    /// <summary>
    /// Stages 501-600: Volcano region
    /// </summary>
    Volcano = 5,

    /// <summary>
    /// Stages 601-700: Ocean region
    /// </summary>
    Ocean = 6,

    /// <summary>
    /// Stages 701-800: Sky region
    /// </summary>
    Sky = 7,

    /// <summary>
    /// Stages 801-900: Underground region
    /// </summary>
    Underground = 8,

    /// <summary>
    /// Stages 901-1000: Cursed Lands region
    /// </summary>
    CursedLands = 9,

    /// <summary>
    /// Stages 10001+: Infinite Land (Endless mode)
    /// No checkpoints
    /// </summary>
    InfiniteLand = 99
}
