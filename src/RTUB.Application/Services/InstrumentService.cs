using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Microsoft.Extensions.Logging;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing instrument inventory
/// </summary>
public class InstrumentService : IInstrumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IImageService _imageService;
    private readonly IInstrumentStorageService _instrumentStorageService;
    private readonly ILogger<InstrumentService> _logger;

    public InstrumentService(
        ApplicationDbContext context, 
        IImageService imageService,
        IInstrumentStorageService instrumentStorageService,
        ILogger<InstrumentService> logger)
    {
        _context = context;
        _imageService = imageService;
        _instrumentStorageService = instrumentStorageService;
        _logger = logger;
    }

    public async Task<Instrument?> GetByIdAsync(int id)
    {
        return await _context.Instruments
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<IEnumerable<Instrument>> GetAllAsync()
    {
        return await _context.Instruments
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Instrument>> GetByCategoryAsync(string category)
    {
        return await _context.Instruments
            .Where(i => i.Category == category)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Instrument>> GetByConditionAsync(InstrumentCondition condition)
    {
        return await _context.Instruments
            .Where(i => i.Condition == condition)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Instrument>> GetByLocationAsync(string location)
    {
        return await _context.Instruments
            .Where(i => i.Location == location)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<Instrument> CreateAsync(Instrument instrument)
    {
        _context.Instruments.Add(instrument);
        await _context.SaveChangesAsync();
        return instrument;
    }

    public async Task UpdateAsync(Instrument instrument)
    {
        _context.Instruments.Update(instrument);
        await _context.SaveChangesAsync();
        
        // Invalidate the cached instrument image so the new image is served immediately
        _imageService.InvalidateInstrumentImageCache(instrument.Id);
    }

    public async Task SetInstrumentImageAsync(int id, byte[]? imageData, string? contentType)
    {
        var instrument = await _context.Instruments.FindAsync(id);
        if (instrument == null)
            throw new InvalidOperationException($"Instrument with ID {id} not found");

        // If imageData is provided, upload to IDrive S3
        if (imageData != null && !string.IsNullOrEmpty(contentType))
        {
            // STRATEGY: Replace-Before-Upload
            // Step 1: Delete old image BEFORE uploading new one
            if (!string.IsNullOrEmpty(instrument.ImageUrl))
            {
                // Extract filename and delete from S3
                var oldFilename = ExtractFilenameFromUrl(instrument.ImageUrl);
                if (!string.IsNullOrEmpty(oldFilename))
                {
                    _logger.LogInformation("Deleting old instrument image from S3: {Filename}", oldFilename);
                    await _instrumentStorageService.DeleteImageAsync(oldFilename);
                }
            }
            
            // Step 2: Upload new image to IDrive S3 and get the filename
            var filename = await _instrumentStorageService.UploadImageAsync(imageData, id, contentType);
            
            // Step 3: Get the public URL from the storage service
            var imageUrl = await _instrumentStorageService.GetImageUrlAsync(filename);
            
            // Step 4: Store the full URL in the ImageUrl field
            instrument.SetImageUrl(imageUrl);
        }
        
        _context.Instruments.Update(instrument);
        await _context.SaveChangesAsync();
        
        // Invalidate the cached instrument image so the new image is served immediately
        _imageService.InvalidateInstrumentImageCache(id);
    }

    public async Task DeleteAsync(int id)
    {
        var instrument = await _context.Instruments.FindAsync(id);
        if (instrument != null)
        {
            // Delete S3 image if exists
            if (!string.IsNullOrEmpty(instrument.ImageUrl))
            {
                // Extract filename from URL and delete from S3
                var filename = ExtractFilenameFromUrl(instrument.ImageUrl);
                if (!string.IsNullOrEmpty(filename))
                {
                    await _instrumentStorageService.DeleteImageAsync(filename);
                }
            }

            _context.Instruments.Remove(instrument);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Dictionary<InstrumentCondition, int>> GetConditionStatsAsync()
    {
        return await _context.Instruments
            .GroupBy(i => i.Condition)
            .Select(g => new { Condition = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Condition, x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetCategoryStatsAsync()
    {
        return await _context.Instruments
            .GroupBy(i => i.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count);
    }

    private string? ExtractFilenameFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        try
        {
            // Extract filename from URL path (last segment after the last '/')
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Split('/');
            return segments.Length > 0 ? segments[^1] : null;
        }
        catch
        {
            return null;
        }
    }
}
