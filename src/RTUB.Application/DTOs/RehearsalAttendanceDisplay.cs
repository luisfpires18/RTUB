using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// Display DTO for rehearsal attendance information
/// Used in Rehearsals page for displaying attendance status
/// </summary>
public class RehearsalAttendanceDisplay
{
    public Rehearsal Rehearsal { get; set; } = null!;
    public RehearsalAttendance? Attendance { get; set; }
    public bool WillAttend => Attendance?.WillAttend ?? false;
    public bool Attended => Attendance?.Attended ?? false;
    public InstrumentType? Instrument => Attendance?.Instrument;
}
