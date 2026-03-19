using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a transport offer attached to a Post in a discussion.
/// Any member can create a transport offer for up to TotalSeats passengers.
/// The post author is the organizer but does not occupy a reserved seat.
/// </summary>
public class Transportation : BaseEntity
{
    /// <summary>1:1 with Post — the post that carries this transport offer.</summary>
    [Required]
    public int PostId { get; set; }

    [Required(ErrorMessage = "A descrição do veículo é obrigatória")]
    [MaxLength(200, ErrorMessage = "A descrição não pode exceder 200 caracteres")]
    public string VehicleDescription { get; set; } = string.Empty;

    [Required]
    [Range(2, 20, ErrorMessage = "O número de lugares deve estar entre 2 e 20")]
    public int TotalSeats { get; set; }

    [MaxLength(500, ErrorMessage = "As notas não podem exceder 500 caracteres")]
    public string? Notes { get; set; }

    public virtual Post Post { get; set; } = null!;
    public virtual ICollection<TransportationPassenger> Passengers { get; set; } = new List<TransportationPassenger>();

    /// <summary>Number of passengers currently registered.</summary>
    public int OccupiedSeats => Passengers.Count;

    /// <summary>Remaining available seats.</summary>
    public int AvailableSeats => TotalSeats - Passengers.Count;

    public static Transportation Create(int postId, string vehicleDescription, int totalSeats, string? notes = null)
    {
        if (postId <= 0) throw new ArgumentException("Post ID must be positive", nameof(postId));
        if (string.IsNullOrWhiteSpace(vehicleDescription)) throw new ArgumentException("Vehicle description cannot be empty", nameof(vehicleDescription));
        if (totalSeats < 2 || totalSeats > 20) throw new ArgumentOutOfRangeException(nameof(totalSeats), "Total seats must be between 2 and 20");

        return new Transportation
        {
            PostId = postId,
            VehicleDescription = vehicleDescription.Trim(),
            TotalSeats = totalSeats,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }

    public void Update(string vehicleDescription, int totalSeats, string? notes)
    {
        VehicleDescription = vehicleDescription.Trim();
        TotalSeats = totalSeats;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}
