namespace RTUB.Application.Services.Geocoding;

/// <summary>
/// Result model from Nominatim geocoding API
/// </summary>
internal class NominatimResult
{
    public string lat { get; set; } = "";
    public string lon { get; set; } = "";
    public string display_name { get; set; } = "";
    public string? type { get; set; }
    public string? @class { get; set; }
    public double importance { get; set; }
    public Address? address { get; set; }
    public List<string>? boundingbox { get; set; }
}
