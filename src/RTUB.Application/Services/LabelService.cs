using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using Microsoft.EntityFrameworkCore;


namespace RTUB.Application.Services;

/// <summary>
/// Label service implementation using Repository pattern
/// Contains business logic for label operations
/// Follows Single Responsibility and Dependency Inversion principles
/// Now depends on ILabelRepository abstraction instead of concrete DbContext
/// </summary>
public class LabelService : ILabelService
{
    private readonly ILabelRepository _labelRepository;

    public LabelService(ILabelRepository labelRepository)
    {
        _labelRepository = labelRepository;
    }

    public async Task<Label?> GetLabelByIdAsync(int id)
    {
        return await _labelRepository.GetByIdAsync(id);
    }

    public async Task<Label?> GetLabelByReferenceAsync(string reference)
    {
        return await _labelRepository.Query()
            .FirstOrDefaultAsync(l => l.Reference == reference && l.IsActive);
    }

    public async Task<IEnumerable<Label>> GetAllLabelsAsync()
    {
        return await _labelRepository.GetAllAsync();
    }

    public async Task<IEnumerable<Label>> GetActiveLabelsAsync()
    {
        return await _labelRepository.Query()
            .AsNoTracking()
            .Where(l => l.IsActive)
            .ToListAsync();
    }

    public async Task<Label> CreateLabelAsync(string reference, string title, string content, bool isActive = true)
    {
        var label = Label.Create(reference, title, content, isActive);
        return await _labelRepository.AddAsync(label);
    }

    public async Task UpdateLabelContentAsync(int id, string title, string content, bool isActive)
    {
        var label = await _labelRepository.GetByIdOrThrowAsync(id);

        label.UpdateContent(title, content, isActive);
        await _labelRepository.UpdateAsync(label);
    }

    public async Task ActivateLabelAsync(int id)
    {
        var label = await _labelRepository.GetByIdOrThrowAsync(id);

        label.Activate();
        await _labelRepository.UpdateAsync(label);
    }

    public async Task DeactivateLabelAsync(int id)
    {
        var label = await _labelRepository.GetByIdOrThrowAsync(id);

        label.Deactivate();
        await _labelRepository.UpdateAsync(label);
    }

    public async Task DeleteLabelAsync(int id)
    {
        var label = await _labelRepository.GetByIdOrThrowAsync(id);

        await _labelRepository.DeleteAsync(label);
    }
}
