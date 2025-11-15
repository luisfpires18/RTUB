using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Label operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface ILabelService
{
    Task<Label?> GetLabelByIdAsync(int id);
    Task<Label?> GetLabelByReferenceAsync(string reference);
    Task<IEnumerable<Label>> GetAllLabelsAsync();
    Task<IEnumerable<Label>> GetActiveLabelsAsync();
    Task<Label> CreateLabelAsync(string reference, string title, string content, bool isActive = true);
    Task UpdateLabelContentAsync(int id, string title, string content, bool isActive);
    Task ActivateLabelAsync(int id);
    Task DeactivateLabelAsync(int id);
    Task DeleteLabelAsync(int id);
}
