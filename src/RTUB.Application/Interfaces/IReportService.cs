using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

public interface IReportService
{
    Task<Report?> GetReportByIdAsync(int id);
    Task<IEnumerable<Report>> GetAllReportsAsync();
    Task<IEnumerable<Report>> GetPublishedReportsAsync();
    Task<Report> CreateReportAsync(string title, int year, string? summary = null);
    Task UpdateReportAsync(int id, string? summary);
    Task PublishReportAsync(int id);
    Task UnpublishReportAsync(int id);
    Task<byte[]> GenerateReportPdfAsync(int reportId);
    Task RecalculateReportFinancialsAsync(int reportId);
    Task DeleteReportAsync(int reportId);
}
