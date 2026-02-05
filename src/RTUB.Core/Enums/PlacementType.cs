namespace RTUB.Core.Enums;

/// <summary>
/// Represents how an enemy should be positioned in battle
/// </summary>
public enum PlacementType
{
    /// <summary>
    /// Ground-based enemy - stands on the floor
    /// </summary>
    Terrestrial = 0,

    /// <summary>
    /// Flying enemy - hovers in the air above ground units
    /// </summary>
    Aerial = 1
}
