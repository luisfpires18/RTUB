namespace RTUB.Application.DTOs;

/// <summary>
/// Form model for editing gallery media
/// Used in Gallery page for editing existing media
/// </summary>
public class GalleryEditModel
{
    public string Title { get; set; } = string.Empty;
    public DateTime SelectedDate { get; set; } = DateTime.Today;
    public List<string> TaggedPeopleIds { get; set; } = new();
    public bool IsPrivate { get; set; } = true;
}
