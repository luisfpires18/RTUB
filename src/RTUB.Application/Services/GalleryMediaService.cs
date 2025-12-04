using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public class GalleryMediaService : IGalleryMediaService
{
    private readonly IGalleryMediaRepository _repository;
    private readonly ApplicationDbContext _context;

    public GalleryMediaService(IGalleryMediaRepository repository, ApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task<IEnumerable<GalleryMedia>> GetAllAsync(int? year = null, string? personId = null, bool? isAuthenticated = null)
    {
        return await _repository.GetAllWithDetailsAsync(year, personId, isAuthenticated);
    }

    public async Task<GalleryMedia?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdWithDetailsAsync(id);
    }

    public async Task<GalleryMedia> CreateAsync(GalleryMedia media)
    {
        await _repository.AddAsync(media);
        await _context.SaveChangesAsync();
        return media;
    }

    public async Task<GalleryMedia> UpdateAsync(GalleryMedia media)
    {
        // Load the existing tracked entity from the context
        var existingMedia = await _context.GalleryMedia
            .FirstOrDefaultAsync(m => m.Id == media.Id);
        
        if (existingMedia == null)
            throw new InvalidOperationException($"Media with ID {media.Id} not found");
        
        // Update only the allowed fields on the tracked entity
        existingMedia.UpdateDetails(media.Title, media.Year, media.Month, media.Day, media.TakenAt);
        existingMedia.UpdatePrivacy(media.IsPrivate);
        
        await _context.SaveChangesAsync();
        return existingMedia;
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        await _context.SaveChangesAsync();
    }

    public async Task AddPersonTagsAsync(int mediaId, IEnumerable<string> personIds)
    {
        var media = await _context.GalleryMedia
            .Include(m => m.PeopleInMedia)
            .FirstOrDefaultAsync(m => m.Id == mediaId);

        if (media == null)
            throw new InvalidOperationException("Media not found");

        foreach (var personId in personIds)
        {
            if (!media.PeopleInMedia.Any(p => p.UserId == personId))
            {
                media.PeopleInMedia.Add(GalleryMediaPersonTag.Create(mediaId, personId));
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task RemovePersonTagsAsync(int mediaId, IEnumerable<string> personIds)
    {
        var media = await _context.GalleryMedia
            .Include(m => m.PeopleInMedia)
            .FirstOrDefaultAsync(m => m.Id == mediaId);

        if (media == null)
            throw new InvalidOperationException("Media not found");

        var tagsToRemove = media.PeopleInMedia
            .Where(p => personIds.Contains(p.UserId))
            .ToList();

        foreach (var tag in tagsToRemove)
        {
            media.PeopleInMedia.Remove(tag);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<int>> GetAvailableYearsAsync(bool? isAuthenticated = null)
    {
        return await _repository.GetAvailableYearsAsync(isAuthenticated);
    }

    public async Task<(IEnumerable<GalleryMedia> Items, int TotalCount)> GetPaginatedAsync(
        int page,
        int pageSize,
        int? year = null,
        string? personId = null,
        bool? isAuthenticated = null)
    {
        return await _repository.GetPaginatedWithDetailsAsync(page, pageSize, year, personId, isAuthenticated);
    }
}
