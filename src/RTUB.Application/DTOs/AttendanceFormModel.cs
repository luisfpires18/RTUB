using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// Form model for marking rehearsal attendance
/// Used in Rehearsals page for attendance management
/// </summary>
public class AttendanceFormModel
{
    public bool WillAttend { get; set; } = true;
    
    public InstrumentType? Instrument { get; set; }
    
    [MaxLength(500, ErrorMessage = "As notas não podem exceder 500 caracteres")]
    public string? Notes { get; set; }
}
