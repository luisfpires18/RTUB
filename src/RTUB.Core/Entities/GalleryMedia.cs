using RTUB.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a media item (image or video) in the gallery
/// Domain entity - contains only business logic, no infrastructure concerns
/// </summary>
public class GalleryMedia : BaseEntity
{
    [Required(ErrorMessage = "O ID do carregador é obrigatório")]
    public string UploaderId { get; set; } = string.Empty;

    [Required(ErrorMessage = "O título é obrigatório")]
    [MaxLength(200, ErrorMessage = "O título não pode exceder 200 caracteres")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "O tipo de media é obrigatório")]
    public MediaType MediaType { get; set; }

    [Required(ErrorMessage = "A URL da media é obrigatória")]
    public string MediaUrl { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    [Required(ErrorMessage = "O ano é obrigatório")]
    public int Year { get; set; }

    [Range(1, 12, ErrorMessage = "O mês deve estar entre 1 e 12")]
    public byte? Month { get; set; }

    [Range(1, 31, ErrorMessage = "O dia deve estar entre 1 e 31")]
    public byte? Day { get; set; }

    public DateTime? TakenAt { get; set; }

    /// <summary>
    /// Indicates if the media is private (visible only to logged-in members)
    /// </summary>
    public bool IsPrivate { get; set; } = true;

    // Navigation properties
    public virtual ApplicationUser Uploader { get; set; } = null!;
    public virtual ICollection<GalleryMediaPersonTag> PeopleInMedia { get; set; } = new List<GalleryMediaPersonTag>();

    // Private constructor for EF Core
    private GalleryMedia() { }

    /// <summary>
    /// Factory method - ensures valid entity creation
    /// </summary>
    public static GalleryMedia Create(
        string uploaderId,
        string title,
        MediaType mediaType,
        string mediaUrl,
        int year,
        byte? month = null,
        byte? day = null,
        string? thumbnailUrl = null,
        DateTime? takenAt = null,
        bool isPrivate = true)
    {
        ValidateUploaderId(uploaderId);
        ValidateTitle(title);
        ValidateMediaUrl(mediaUrl);
        ValidateYear(year);
        ValidateMonth(month);
        ValidateDay(day);

        var now = DateTime.UtcNow;
        return new GalleryMedia
        {
            UploaderId = uploaderId,
            Title = title,
            MediaType = mediaType,
            MediaUrl = mediaUrl,
            Year = year,
            Month = month,
            Day = day,
            ThumbnailUrl = thumbnailUrl,
            TakenAt = takenAt,
            IsPrivate = isPrivate,
            CreatedAt = now
        };
    }

    /// <summary>
    /// Update media details
    /// </summary>
    public void UpdateDetails(string title, int year, byte? month = null, byte? day = null, DateTime? takenAt = null)
    {
        ValidateTitle(title);
        ValidateYear(year);
        ValidateMonth(month);
        ValidateDay(day);

        Title = title;
        Year = year;
        Month = month;
        Day = day;
        TakenAt = takenAt;
    }

    /// <summary>
    /// Update media URL
    /// </summary>
    public void UpdateMediaUrl(string mediaUrl)
    {
        ValidateMediaUrl(mediaUrl);
        MediaUrl = mediaUrl;
    }

    /// <summary>
    /// Set or update thumbnail URL for video media
    /// </summary>
    public void SetThumbnailUrl(string? thumbnailUrl)
    {
        ThumbnailUrl = thumbnailUrl;
    }

    /// <summary>
    /// Update privacy setting
    /// </summary>
    public void UpdatePrivacy(bool isPrivate)
    {
        IsPrivate = isPrivate;
    }

    // Validation methods
    private static void ValidateUploaderId(string uploaderId)
    {
        if (string.IsNullOrWhiteSpace(uploaderId))
            throw new ArgumentException("O ID do carregador não pode estar vazio", nameof(uploaderId));
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título não pode estar vazio", nameof(title));

        if (title.Length > 200)
            throw new ArgumentException("O título não pode exceder 200 caracteres", nameof(title));
    }

    private static void ValidateMediaUrl(string mediaUrl)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
            throw new ArgumentException("A URL da media não pode estar vazia", nameof(mediaUrl));
    }

    private static void ValidateYear(int year)
    {
        const int minYear = 1900;
        var maxYear = DateTime.UtcNow.Year + 1;

        if (year < minYear || year > maxYear)
            throw new ArgumentException($"O ano deve estar entre {minYear} e {maxYear}", nameof(year));
    }

    private static void ValidateMonth(byte? month)
    {
        if (month.HasValue && (month.Value < 1 || month.Value > 12))
            throw new ArgumentException("O mês deve estar entre 1 e 12", nameof(month));
    }

    private static void ValidateDay(byte? day)
    {
        if (day.HasValue && (day.Value < 1 || day.Value > 31))
            throw new ArgumentException("O dia deve estar entre 1 e 31", nameof(day));
    }
}
