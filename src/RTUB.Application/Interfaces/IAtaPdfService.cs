using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for generating meeting minutes (Ata) PDFs
/// </summary>
public interface IAtaPdfService
{
    /// <summary>
    /// Generates a PDF document for meeting minutes
    /// </summary>
    /// <param name="ata">The meeting ata with all related data</param>
    /// <returns>PDF bytes</returns>
    byte[] GenerateAtaPdf(MeetingAta ata);
}
