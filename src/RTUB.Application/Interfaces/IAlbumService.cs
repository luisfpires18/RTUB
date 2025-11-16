using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

public interface IAlbumService
{
    Task<Album?> GetAlbumByIdAsync(int id);
    Task<IEnumerable<Album>> GetAllAlbumsAsync();
    Task<IEnumerable<Album>> GetPublicAlbumsAsync();
    Task<IEnumerable<Album>> GetAlbumsWithSongsAsync();
    Task<Album?> GetAlbumWithSongsAsync(int id);
    Task<Album> CreateAlbumAsync(string title, int? year, string? description = null, string? imageUrl = null, bool isPrivate = false);
    Task UpdateAlbumAsync(int id, string title, int? year, string? description, bool isPrivate);
    Task UpdateAlbumWithCoverAsync(int id, string title, int? year, string? description, bool isPrivate, Stream imageStream, string fileName, string contentType);
    Task SetAlbumCoverAsync(int id, Stream imageStream, string fileName, string contentType);
    Task DeleteAlbumAsync(int id);
}
