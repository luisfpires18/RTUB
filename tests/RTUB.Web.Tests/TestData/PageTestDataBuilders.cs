using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Web.Tests.TestData;

/// <summary>
/// Shared test data builders for Web page component tests.
/// Use these to keep entity creation consistent across Albums, Songs, Meetings, Rehearsals, etc.
/// </summary>
public static class PageTestDataBuilders
{
    public static Album CreateAlbum(int id, string title, int year, bool isPrivate = false)
    {
        var a = Core.Entities.Album.Create(
            title: title,
            year: year,
            description: $"Description for {title}",
            isPrivate: isPrivate);
        a.Id = id;
        return a;
    }

    public static Song CreateSong(int id, string title, int albumId, int? trackNumber = null)
    {
        var s = Core.Entities.Song.Create(
            title: title,
            albumId: albumId,
            trackNumber: trackNumber);
        s.Id = id;
        return s;
    }

    public static Meeting CreateMeeting(int id, string title, DateTime date, MeetingType type = MeetingType.AssembleiaGeralOrdinaria)
    {
        return new Meeting
        {
            Id = id,
            Type = type,
            Title = title,
            Date = date,
            Location = "Test Location",
            Statement = $"Statement for {title}",
            OrganizerUserId = "test-organizer-id"
        };
    }

    public static Rehearsal CreateRehearsal(int id, DateTime date, string location, string? theme = null)
    {
        var r = Rehearsal.Create(date: date, location: location, theme: theme);
        r.Id = id;
        return r;
    }

    public static Slideshow CreateSlideshow(int id, string title, int order = 1, string description = "", int intervalMs = 5000)
    {
        var s = Slideshow.Create(title, order, description, intervalMs);
        s.Id = id;
        return s;
    }

    public static Request CreateRequest(int id, string name, string email, string eventType, DateTime preferredDate, string location, RequestStatus status = RequestStatus.Pending)
    {
        var r = Request.Create(name, email, "123456789", eventType, preferredDate, location, "Message");
        r.Id = id;
        r.Status = status;
        return r;
    }
}
