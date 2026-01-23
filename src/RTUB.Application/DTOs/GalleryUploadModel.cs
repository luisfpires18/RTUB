namespace RTUB.Application.DTOs;

/// <summary>
/// Form model for uploading gallery media
/// Used in Gallery page for media uploads
/// </summary>
public class GalleryUploadModel
{
    public string Title { get; set; } = string.Empty;
    public DateTime SelectedDate { get; set; } = DateTime.Today;
    public List<string> TaggedPeopleIds { get; set; } = new();
    public string? PreviewUrl { get; set; }
    public bool IsPrivate { get; set; } = true;
}
