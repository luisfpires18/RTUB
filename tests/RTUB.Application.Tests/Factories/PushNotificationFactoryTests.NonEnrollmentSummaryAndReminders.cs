using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Factories;

// PushNotificationFactoryTests: Non Enrollment Summary And Reminders tests.
public partial class PushNotificationFactoryTests
{
    // Event Non-Enrollment Notification Tests

    [Fact]
    public void CreateEventNonEnrollmentNotification_ReturnsCorrectNotification()
    {
        // Arrange
        var eventDate = new DateTime(2024, 12, 25, 19, 30, 0);
        var testEvent = new Event
        {
            Id = 123,
            Name = "Atuação de Natal",
            Location = "Centro Cultural",
            Date = eventDate,
            Type = Core.Enums.EventType.Atuacao
        };
        var userDisplayName = "João Silva";

        // Act
        var notification = _factory.CreateEventNonEnrollmentNotification(testEvent, userDisplayName);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal("Não vai ao evento", notification.Title);
        Assert.Contains("João Silva não vai a", notification.Body);
        Assert.Contains("Atuação de Natal", notification.Body);
        Assert.Equal("/icons/rtub-logo-192.png", notification.Icon);
        Assert.Equal("/events/123", notification.Url);
        Assert.Equal("event-non-enrollment-123", notification.Tag);
    }

    [Fact]
    public void CreateEventNonEnrollmentNotification_NullEvent_ThrowsArgumentNullException()
    {
        // Arrange
        Event? nullEvent = null;
        var userDisplayName = "Test User";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateEventNonEnrollmentNotification(nullEvent!, userDisplayName));
    }

    [Fact]
    public void CreateEventNonEnrollmentNotification_NullUserDisplayName_ThrowsArgumentNullException()
    {
        // Arrange
        var testEvent = new Event
        {
            Id = 1,
            Name = "Test Event",
            Location = "Test Location",
            Date = DateTime.UtcNow.Date.AddDays(1),
            Type = Core.Enums.EventType.Festival
        };
        string? nullUserDisplayName = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateEventNonEnrollmentNotification(testEvent, nullUserDisplayName!));
    }

    [Fact]
    public void CreateEventNonEnrollmentNotification_EmptyUserDisplayName_ThrowsArgumentException()
    {
        // Arrange
        var testEvent = new Event
        {
            Id = 1,
            Name = "Test Event",
            Location = "Test Location",
            Date = DateTime.UtcNow.Date.AddDays(1),
            Type = Core.Enums.EventType.Festival
        };
        var emptyUserDisplayName = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _factory.CreateEventNonEnrollmentNotification(testEvent, emptyUserDisplayName));
    }


    [Fact]
    public void CreateWeeklySummaryNotification_WithAllActivities_ReturnsCorrectNotification()
    {
        // Arrange
        var eventCount = 2;
        var rehearsalCount = 3;
        var meetingCount = 1;

        // Act
        var notification = _factory.CreateWeeklySummaryNotification(eventCount, rehearsalCount, meetingCount);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal("Resumo Semanal", notification.Title);
        Assert.Equal("Esta semana tens: 2 atuações, 3 ensaios, 1 reunião", notification.Body);
        Assert.Equal("/icons/rtub-logo-192.png", notification.Icon);
        Assert.Equal("/events", notification.Url);
        Assert.Equal("weekly-summary", notification.Tag);
    }

    [Fact]
    public void CreateWeeklySummaryNotification_WithOnlyEvents_ReturnsCorrectNotification()
    {
        // Arrange
        var eventCount = 1;
        var rehearsalCount = 0;
        var meetingCount = 0;

        // Act
        var notification = _factory.CreateWeeklySummaryNotification(eventCount, rehearsalCount, meetingCount);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal("Resumo Semanal", notification.Title);
        Assert.Equal("Esta semana tens: 1 atuação", notification.Body);
        Assert.Equal("/events", notification.Url);
    }

    [Fact]
    public void CreateWeeklySummaryNotification_WithOnlyRehearsals_ReturnsCorrectNotification()
    {
        // Arrange
        var eventCount = 0;
        var rehearsalCount = 2;
        var meetingCount = 0;

        // Act
        var notification = _factory.CreateWeeklySummaryNotification(eventCount, rehearsalCount, meetingCount);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal("Resumo Semanal", notification.Title);
        Assert.Equal("Esta semana tens: 2 ensaios", notification.Body);
        Assert.Equal("/rehearsals", notification.Url);
    }

    [Fact]
    public void CreateWeeklySummaryNotification_WithOnlyMeetings_ReturnsCorrectNotification()
    {
        // Arrange
        var eventCount = 0;
        var rehearsalCount = 0;
        var meetingCount = 2;

        // Act
        var notification = _factory.CreateWeeklySummaryNotification(eventCount, rehearsalCount, meetingCount);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal("Resumo Semanal", notification.Title);
        Assert.Equal("Esta semana tens: 2 reuniões", notification.Body);
        Assert.Equal("/meetings", notification.Url);
    }

    [Fact]
    public void CreateWeeklySummaryNotification_WithNoActivities_ReturnsCorrectNotification()
    {
        // Arrange
        var eventCount = 0;
        var rehearsalCount = 0;
        var meetingCount = 0;

        // Act
        var notification = _factory.CreateWeeklySummaryNotification(eventCount, rehearsalCount, meetingCount);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal("Resumo Semanal", notification.Title);
        Assert.Equal("Não há atividades agendadas para esta semana.", notification.Body);
        Assert.Equal("/", notification.Url);
    }

    [Fact]
    public void CreateWeeklySummaryNotification_WithSingularCounts_UsesCorrectGrammar()
    {
        // Arrange
        var eventCount = 1;
        var rehearsalCount = 1;
        var meetingCount = 1;

        // Act
        var notification = _factory.CreateWeeklySummaryNotification(eventCount, rehearsalCount, meetingCount);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal("Esta semana tens: 1 atuação, 1 ensaio, 1 reunião", notification.Body);
    }


    [Fact]
    public void CreateMeetingRequestRejectedNotification_Expired_ReturnsExpirationMessage()
    {
        // Arrange
        var request = new MeetingRequest
        {
            Id = 15,
            Title = "Reunião extraordinária",
            ProposedDateTime = DateTime.UtcNow.AddDays(-2)
        };

        // Act
        var notification = _factory.CreateMeetingRequestRejectedNotification(request, isExpired: true);

        // Assert
        Assert.Equal("Pedido de reunião rejeitado", notification.Title);
        Assert.Contains("expirou porque a data proposta já passou", notification.Body);
        Assert.Equal("/meetings", notification.Url);
        Assert.Equal("meeting-request-rejected-15", notification.Tag);
    }

    [Fact]
    public void CreateMeetingRequestRejectedNotification_Rejected_ReturnsRejectionMessage()
    {
        // Arrange
        var request = new MeetingRequest
        {
            Id = 21,
            Title = "Reunião de Direção",
            ProposedDateTime = DateTime.UtcNow.AddDays(5)
        };

        // Act
        var notification = _factory.CreateMeetingRequestRejectedNotification(request, isExpired: false);

        // Assert
        Assert.Equal("Pedido de reunião rejeitado", notification.Title);
        Assert.Contains("foi rejeitado", notification.Body);
        Assert.Equal("/meetings", notification.Url);
        Assert.Equal("meeting-request-rejected-21", notification.Tag);
    }

    [Fact]
    public void CreateCalotesReminderNotification_OpensTheReactCalotesPage()
    {
        var notification = _factory.CreateCalotesReminderNotification(12.5m);

        Assert.Equal("/treasury/calotes", notification.Url);
        Assert.Equal("calotes-reminder", notification.Tag);
    }

    [Fact]
    public void ItemNotifications_OpenTheItemItself_WithARelativeLink()
    {
        // Task 058: a tap lands on the event / rehearsal / meeting / album / gallery item, never on a list or another host.
        var e = new Event { Id = 7, Name = "Serenata", Location = "Sé", Date = DateTime.Today.AddDays(3) };
        var r = new Rehearsal { Id = 3, Date = DateTime.Today.AddDays(1), Location = "Sala" };
        var m = new Meeting { Id = 5, Title = "AG", Type = MeetingType.AssembleiaGeralOrdinaria, Date = DateTime.Today.AddDays(2) };

        Assert.Equal("/events/7", _factory.CreateEventNotification(e, isReminder: false).Url);
        Assert.Equal("/events/7", _factory.CreateEventNotification(e, isReminder: true).Url);
        Assert.Equal("/events/7", _factory.CreateEventEnrollmentNotification(e, "Zé").Url);
        Assert.Equal("/events/7", _factory.CreateEventVideoUploadNotification(e, "Zé").Url);
        Assert.Equal("/events/7/discussion", _factory.CreateDiscussionPostNotification(e, "Zé", "Post").Url);
        Assert.Equal("/events/7/discussion", _factory.CreateDiscussionCommentNotification(e, "Zé", "Post").Url);
        Assert.Equal("/events/7/discussion", _factory.CreateMentionNotification(e, "Zé", "Post", isComment: true).Url);
        Assert.Equal("/rehearsals/3", _factory.CreateRehearsalReminderNotification(r).Url);
        Assert.Equal("/rehearsals/3", _factory.CreateRehearsalAttendanceApprovalNotification(r, "Zé").Url);
        Assert.Equal("/meetings?meeting=5", _factory.CreateMeetingNotification(m, isReminder: false).Url);
        Assert.Equal("/meetings?meeting=5", _factory.CreateMeetingNotification(m, isReminder: true).Url);
        Assert.Equal("/meetings", _factory.CreateMeetingNotification(new Meeting { Title = "Pedido", Date = m.Date }, isReminder: false).Url);
        Assert.Equal("/music/albums/9", _factory.CreateSongVideoUploadNotification(new Song { Id = 1, AlbumId = 9, Title = "Fado" }, "Zé").Url);
        Assert.Equal("/naipes?instrument=Bandolim", _factory.CreateNaipeContentNotification("Solo", InstrumentType.Bandolim, isVideo: true).Url);
        Assert.Equal("/gallery?item=11", _factory.CreateGalleryTagNotification(11).Url);
        Assert.Equal("/messages?c=4", _factory.CreateConversationMessageNotification("Zé", null, 4).Url);
    }
}
