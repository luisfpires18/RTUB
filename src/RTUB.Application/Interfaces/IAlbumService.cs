using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

public interface IAlbumService
{
    Task<Album?> GetAlbumByIdAsync(int id);
    Task<IEnumerable<Album>> GetAllAlbumsAsync();
    Task<IEnumerable<Album>> GetAlbumsWithSongsAsync();
    Task<Album?> GetAlbumWithSongsAsync(int id);
    Task<Album> CreateAlbumAsync(string title, int? year, string? description = null);
    Task<Album> CreateAlbumWithCoverAsync(string title, int? year, string? description, Stream imageStream, string fileName, string contentType);
    Task UpdateAlbumAsync(int id, string title, int? year, string? description);
    Task UpdateAlbumWithCoverAsync(int id, string title, int? year, string? description, Stream imageStream, string fileName, string contentType);
    Task SetAlbumCoverAsync(int id, Stream imageStream, string fileName, string contentType);
    Task DeleteAlbumAsync(int id);
}
