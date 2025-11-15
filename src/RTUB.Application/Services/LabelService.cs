using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;


namespace RTUB.Application.Services;

/// <summary>
/// Label service implementation
/// Contains business logic for label operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class LabelService : ILabelService
{
    private readonly ApplicationDbContext _context;

    public LabelService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Label?> GetLabelByIdAsync(int id)
    {
        return await _context.Labels.FindAsync(id);
    }

    public async Task<Label?> GetLabelByReferenceAsync(string reference)
    {
        return await _context.Labels
            .FirstOrDefaultAsync(l => l.Reference == reference && l.IsActive);
    }

    public async Task<IEnumerable<Label>> GetAllLabelsAsync()
    {
        return await _context.Labels.ToListAsync();
    }

    public async Task<IEnumerable<Label>> GetActiveLabelsAsync()
    {
        return await _context.Labels
            .Where(l => l.IsActive)
            .ToListAsync();
    }

    public async Task<Label> CreateLabelAsync(string reference, string title, string content)
    {
        var label = Label.Create(reference, title, content);
        _context.Labels.Add(label);
        await _context.SaveChangesAsync();
        return label;
    }

    public async Task UpdateLabelContentAsync(int id, string title, string content)
    {
        var label = await _context.Labels.FindAsync(id);
        if (label == null)
            throw new InvalidOperationException($"Label with ID {id} not found");

        label.UpdateContent(title, content);
        _context.Labels.Update(label);
        await _context.SaveChangesAsync();
    }

    public async Task ActivateLabelAsync(int id)
    {
        var label = await _context.Labels.FindAsync(id);
        if (label == null)
            throw new InvalidOperationException($"Label with ID {id} not found");

        label.Activate();
        _context.Labels.Update(label);
        await _context.SaveChangesAsync();
    }

    public async Task DeactivateLabelAsync(int id)
    {
        var label = await _context.Labels.FindAsync(id);
        if (label == null)
            throw new InvalidOperationException($"Label with ID {id} not found");

        label.Deactivate();
        _context.Labels.Update(label);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteLabelAsync(int id)
    {
        var label = await _context.Labels.FindAsync(id);
        if (label == null)
            throw new InvalidOperationException($"Label with ID {id} not found");

        _context.Labels.Remove(label);
        await _context.SaveChangesAsync();
    }
}
