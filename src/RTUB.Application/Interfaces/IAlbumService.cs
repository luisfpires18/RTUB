using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

public interface IAlbumService
{
    Task<Album?> GetAlbumByIdAsync(int id);
    Task<IEnumerable<Album>> GetAllAlbumsAsync();
    Task<IEnumerable<Album>> GetAlbumsWithSongsAsync();
    Task<Album?> GetAlbumWithSongsAsync(int id);
    Task<Album> CreateAlbumAsync(string title, int? year, string? description = null);
    Task UpdateAlbumAsync(int id, string title, int? year, string? description);
    Task SetAlbumCoverAsync(int id, byte[]? imageData, string? contentType, string url = "");
    Task DeleteAlbumAsync(int id);
}
