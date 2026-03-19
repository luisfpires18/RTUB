using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

public class TransportationPassenger : BaseEntity
{
    [Required]
    public int TransportationId { get; set; }

    [Required]
    public string PassengerId { get; set; } = string.Empty;

    public virtual Transportation Transportation { get; set; } = null!;
    public virtual ApplicationUser Passenger { get; set; } = null!;

    public static TransportationPassenger Create(int transportationId, string passengerId)
    {
        if (transportationId <= 0) throw new ArgumentException("Transportation ID must be positive", nameof(transportationId));
        if (string.IsNullOrWhiteSpace(passengerId)) throw new ArgumentException("Passenger ID cannot be empty", nameof(passengerId));

        return new TransportationPassenger
        {
            TransportationId = transportationId,
            PassengerId = passengerId
        };
    }
}
