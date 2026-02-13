namespace RTUB.Core.Helpers;

/// <summary>
/// Provides icon and color mappings for biomes and survive mode biomes.
/// </summary>
public static class BiomeDisplayHelper
{
    public static string GetBiomeIcon(string biomeName) => biomeName switch
    {
        "Forest" => "bi-tree-fill",
        "Swamp" => "bi-droplet-fill",
        "Mountains" => "bi-triangle-fill",
        "Snowy" => "bi-snow2",
        "Tropical" => "bi-sun-fill",
        "Caverns" => "bi-gem",
        "Desert" => "bi-brightness-high-fill",
        "Volcanic" => "bi-fire",
        "Ruins" => "bi-building",
        "Dark" => "bi-moon-fill",
        "Light" => "bi-brightness-alt-high-fill",
        "Void" => "bi-infinity",
        _ => "bi-geo-alt-fill"
    };

    public static string GetBiomeColor(string biomeName) => biomeName switch
    {
        "Forest" => "#4caf50",
        "Swamp" => "#8bc34a",
        "Mountains" => "#78909c",
        "Snowy" => "#81d4fa",
        "Tropical" => "#ff9800",
        "Caverns" => "#9c27b0",
        "Desert" => "#ffb74d",
        "Volcanic" => "#f44336",
        "Ruins" => "#795548",
        "Dark" => "#37474f",
        "Light" => "#fdd835",
        "Void" => "#7c4dff",
        _ => "#6f42c1"
    };

    public static string GetSurviveBiomeIcon(string name) => name switch
    {
        "Forest" => "bi-tree-fill", "Swamp" => "bi-droplet-fill", "Mountains" => "bi-triangle-fill",
        "Snowy" => "bi-snow2", "Tropical" => "bi-sun-fill", "Caverns" => "bi-gem",
        "Desert" => "bi-thermometer-sun", "Volcanic" => "bi-fire", "Ruins" => "bi-building",
        "Dark" => "bi-moon-fill", "Light" => "bi-brightness-alt-high-fill", "Void" => "bi-radioactive", _ => "bi-question-circle"
    };

    public static string GetSurviveBiomeColor(string name) => name switch
    {
        "Forest" => "#43a047", "Swamp" => "#689f38", "Mountains" => "#78909c",
        "Snowy" => "#90caf9", "Tropical" => "#e6a200", "Caverns" => "#7e57c2",
        "Desert" => "#ff8f00", "Volcanic" => "#e53935", "Ruins" => "#8d6e63",
        "Dark" => "#9575cd", "Light" => "#fdd835", "Void" => "#e040fb", _ => "#888"
    };
}
