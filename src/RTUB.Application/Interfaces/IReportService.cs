using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

public interface IReportService
{
    Task<Report?> GetReportByIdAsync(int id);
    Task<Report> CreateReportAsync(string title, int year, string? summary = null);
    Task PublishReportAsync(int id);
    Task DeleteReportAsync(int reportId);
}
