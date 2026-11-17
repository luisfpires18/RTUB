using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Factories;

// PushNotificationFactoryTests: Rehearsal And Custom Notifications tests.
public partial class PushNotificationFactoryTests
{
    // Rehearsal Notification Tests

    [Fact]
    public void CreateRehearsalNotification_ReturnsCorrectNotification()
    {
        // Arrange
        var rehearsalDate = new DateTime(2024, 11, 26, 21, 30, 0); // Tuesday, Nov 26, 2024
        var rehearsal = Rehearsal.Create(rehearsalDate, "Centro Académico");
        rehearsal.Id = 789;
        var customBody = "Ensaio importante! Vamos preparar o repertório de Natal.";

        // Act
        var notification = _factory.CreateRehearsalNotification(rehearsal, customBody);

        // Assert
        Assert.NotNull(notification);
        Assert.Contains("Ensaio - ", notification.Title);
        Assert.Contains("26", notification.Title); // Day
        Assert.Contains("nov", notification.Title.ToLower()); // Month abbreviation
        Assert.Contains("terça", notification.Title.ToLower()); // Day of week
        Assert.Equal(customBody, notification.Body);
        Assert.Equal("/icons/rtub-logo-192.png", notification.Icon);
        Assert.Equal("/rehearsals/789", notification.Url);
        Assert.Equal("rehearsal-789", notification.Tag);
    }

    [Fact]
    public void CreateRehearsalNotification_DifferentDay_FormatsCorrectly()
    {
        // Arrange
        var rehearsalDate = new DateTime(2024, 11, 28, 21, 30, 0); // Thursday, Nov 28, 2024
        var rehearsal = Rehearsal.Create(rehearsalDate, "Centro Académico");
        rehearsal.Id = 999;
        var customBody = "Ensaio de quinta-feira.";

        // Act
        var notification = _factory.CreateRehearsalNotification(rehearsal, customBody);

        // Assert
        Assert.Contains("Ensaio - ", notification.Title);
        Assert.Contains("28", notification.Title);
        Assert.Contains("quinta", notification.Title.ToLower()); // Thursday in Portuguese
    }

    [Fact]
    public void CreateRehearsalNotification_NullRehearsal_ThrowsArgumentNullException()
    {
        // Arrange
        Rehearsal? nullRehearsal = null;
        var customBody = "Test body";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateRehearsalNotification(nullRehearsal!, customBody));
    }

    [Fact]
    public void CreateRehearsalNotification_NullCustomBody_ThrowsArgumentNullException()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.UtcNow.Date.AddDays(1), "Centro Académico");
        rehearsal.Id = 1;
        string? nullBody = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateRehearsalNotification(rehearsal, nullBody!));
    }

    [Fact]
    public void CreateRehearsalNotification_EmptyCustomBody_ThrowsArgumentException()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.UtcNow.Date.AddDays(1), "Centro Académico");
        rehearsal.Id = 1;
        var emptyBody = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _factory.CreateRehearsalNotification(rehearsal, emptyBody));
    }


    // Event custom notification tests

    [Fact]
    public void CreateEventCustomNotification_ReturnsCorrectNotification()
    {
        // Arrange
        var testEvent = Event.Create("Test Event", DateTime.UtcNow.AddDays(5), "Test Location", EventType.Atuacao, "Desc");
        testEvent.Id = 123;
        var customBody = "Mensagem personalizada para a atuação.";

        // Act
        var notification = _factory.CreateEventCustomNotification(testEvent, customBody);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal(testEvent.Name, notification.Title);
        Assert.Equal(customBody, notification.Body);
        Assert.Equal("/icons/rtub-logo-192.png", notification.Icon);
        Assert.Equal("/events/123", notification.Url);
        Assert.Equal("event-custom-123", notification.Tag);
    }

    [Fact]
    public void CreateEventCustomNotification_ThrowsOnInvalidArguments()
    {
        var testEvent = Event.Create("Test Event", DateTime.UtcNow.AddDays(5), "Test Location", EventType.Atuacao, "Desc");
        testEvent.Id = 1;

        Assert.Throws<ArgumentNullException>(() => _factory.CreateEventCustomNotification(null!, "body"));
        Assert.Throws<ArgumentNullException>(() => _factory.CreateEventCustomNotification(testEvent, null!));
        Assert.Throws<ArgumentException>(() => _factory.CreateEventCustomNotification(testEvent, string.Empty));
    }

    // Meeting custom notification tests

    [Fact]
    public void CreateMeetingCustomNotification_ReturnsCorrectNotification()
    {
        // Arrange
        var meeting = new Meeting
        {
            Id = 42,
            Title = "Reunião Geral",
            Date = new DateTime(2025, 5, 10, 21, 0, 0),
            Type = MeetingType.AssembleiaGeralOrdinaria
        };
        var customBody = "Mensagem personalizada para a reunião.";

        // Act
        var notification = _factory.CreateMeetingCustomNotification(meeting, customBody);

        // Assert
        Assert.NotNull(notification);
        Assert.Contains("Reunião - ", notification.Title);
        Assert.Equal(customBody, notification.Body);
        Assert.Equal("/icons/rtub-logo-192.png", notification.Icon);
        Assert.Equal("/meetings?meeting=42", notification.Url);
        Assert.Equal("meeting-custom-42", notification.Tag);
    }

    [Fact]
    public void CreateMeetingCustomNotification_ThrowsOnInvalidArguments()
    {
        var meeting = new Meeting
        {
            Id = 1,
            Title = "Reunião Teste",
            Date = DateTime.UtcNow.AddDays(1),
            Type = MeetingType.AssembleiaGeralOrdinaria
        };

        Assert.Throws<ArgumentNullException>(() => _factory.CreateMeetingCustomNotification(null!, "body"));
        Assert.Throws<ArgumentNullException>(() => _factory.CreateMeetingCustomNotification(meeting, null!));
        Assert.Throws<ArgumentException>(() => _factory.CreateMeetingCustomNotification(meeting, string.Empty));
    }

    // Rehearsal Non-Attendance Notification Tests

    [Fact]
    public void CreateRehearsalNonAttendanceNotification_ReturnsCorrectNotification()
    {
        // Arrange
        var rehearsalDate = new DateTime(2024, 11, 26, 21, 30, 0); // Tuesday, Nov 26, 2024
        var rehearsal = Rehearsal.Create(rehearsalDate, "Centro Académico");
        rehearsal.Id = 123;
        var userDisplayName = "João Silva";

        // Act
        var notification = _factory.CreateRehearsalNonAttendanceNotification(rehearsal, userDisplayName);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal("Não vai ao ensaio", notification.Title);
        Assert.Contains("João Silva não vai ao", notification.Body);
        Assert.Contains("Ensaio - ", notification.Body);
        Assert.Equal("/icons/rtub-logo-192.png", notification.Icon);
        Assert.Equal("/rehearsals/123", notification.Url);
        Assert.Equal("rehearsal-non-attendance-123", notification.Tag);
    }

    [Fact]
    public void CreateRehearsalNonAttendanceNotification_NullRehearsal_ThrowsArgumentNullException()
    {
        // Arrange
        Rehearsal? nullRehearsal = null;
        var userDisplayName = "Test User";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateRehearsalNonAttendanceNotification(nullRehearsal!, userDisplayName));
    }

    [Fact]
    public void CreateRehearsalNonAttendanceNotification_NullUserDisplayName_ThrowsArgumentNullException()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.UtcNow.Date.AddDays(1), "Centro Académico");
        rehearsal.Id = 1;
        string? nullUserDisplayName = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateRehearsalNonAttendanceNotification(rehearsal, nullUserDisplayName!));
    }

    [Fact]
    public void CreateRehearsalNonAttendanceNotification_EmptyUserDisplayName_ThrowsArgumentException()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.UtcNow.Date.AddDays(1), "Centro Académico");
        rehearsal.Id = 1;
        var emptyUserDisplayName = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _factory.CreateRehearsalNonAttendanceNotification(rehearsal, emptyUserDisplayName));
    }
}
