namespace RTUB.Application.Services.Geocoding;

/// <summary>
/// Address component from Nominatim result
/// </summary>
internal class Address
{
    public string? city { get; set; }
    public string? town { get; set; }
    public string? village { get; set; }
    public string? hamlet { get; set; }
    public string? municipality { get; set; }
    public string? country { get; set; }
}
