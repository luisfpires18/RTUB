namespace RTUB.Application.DTOs;

/// <summary>
/// Form model for product reservations
/// Used in Shop page for product reservations
/// </summary>
public class ReservationModel
{
    public string? DisplayName { get; set; }
    public bool HasSizes { get; set; }
    public string? Size { get; set; }
}
