using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for naipe type configuration
/// </summary>
public class NaipeTypeConfigDto
{
    public int Id { get; set; }
    public InstrumentType InstrumentType { get; set; }
    public string InstrumentTypeName { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }
    public bool IsVisible { get; set; }
    public int SortOrder { get; set; }
}
