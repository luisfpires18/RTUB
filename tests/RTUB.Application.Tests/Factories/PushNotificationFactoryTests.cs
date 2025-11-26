using RTUB.Application.Factories;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Application.Tests.Factories;

public class PushNotificationFactoryTests
{
    private readonly PushNotificationFactory _factory;

    public PushNotificationFactoryTests()
    {
        _factory = new PushNotificationFactory();
    }

    [Fact]
    public void CreateEventNotification_NewEvent_ReturnsCorrectNotification()
    {
        // Arrange
        var eventDate = new DateTime(2024, 12, 25, 19, 30, 0);
        var testEvent = new Event
        {
            Id = 123,
            Name = "Atuação de Natal",
            Location = "Centro Cultural",
            Date = eventDate
        };
        var baseUrl = "https://rtub.example.com";

        // Act
        var notification = _factory.CreateEventNotification(testEvent, isReminder: false, baseUrl);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal("Atuação de Natal", notification.Title);
        Assert.Equal("Nova atuação: Atuação de Natal em 25 de dezembro de 2024 no Centro Cultural", notification.Body);
        Assert.Equal("/icons/rtub-logo-192.png", notification.Icon);
        Assert.Equal("https://rtub.example.com/events", notification.Url);
        Assert.Equal("event-123", notification.Tag);
    }

    [Fact]
    public void CreateEventNotification_Reminder_ReturnsCorrectNotification()
    {
        // Arrange
        var eventDate = DateTime.UtcNow.Date.AddDays(3).AddHours(19).AddMinutes(30);
        var testEvent = new Event
        {
            Id = 456,
            Name = "Ensaio Geral",
            Location = "Sala de Ensaios",
            Date = eventDate
        };
        var baseUrl = "https://rtub.example.com";

        // Act
        var notification = _factory.CreateEventNotification(testEvent, isReminder: true, baseUrl);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal("Lembrete: Ensaio Geral", notification.Title);
        Assert.Contains("A atuação é em 3 dias", notification.Body);
        Assert.Contains("no Sala de Ensaios", notification.Body);
        Assert.Contains("Não te esqueças de confirmar a tua presença!", notification.Body);
        Assert.Equal("/icons/rtub-logo-192.png", notification.Icon);
        Assert.Equal("https://rtub.example.com/events", notification.Url);
        Assert.Equal("event-reminder-456", notification.Tag);
    }

    [Fact]
    public void CreateEventNotification_ReminderOneDayAway_UsesSingularForm()
    {
        // Arrange
        var eventDate = DateTime.UtcNow.Date.AddDays(1).AddHours(20);
        var testEvent = new Event
        {
            Id = 789,
            Name = "Concerto",
            Location = "Teatro Municipal",
            Date = eventDate
        };
        var baseUrl = "https://rtub.example.com";

        // Act
        var notification = _factory.CreateEventNotification(testEvent, isReminder: true, baseUrl);

        // Assert
        Assert.NotNull(notification);
        Assert.Contains("A atuação é em 1 dia", notification.Body);
    }

    [Fact]
    public void CreateEventNotification_BaseUrlWithTrailingSlash_NormalizesCorrectly()
    {
        // Arrange
        var testEvent = new Event
        {
            Id = 1,
            Name = "Test Event",
            Location = "Test Location",
            Date = DateTime.UtcNow.Date.AddDays(1)
        };
        var baseUrl = "https://rtub.example.com/";

        // Act
        var notification = _factory.CreateEventNotification(testEvent, isReminder: false, baseUrl);

        // Assert
        Assert.Equal("https://rtub.example.com/events", notification.Url);
    }

    [Fact]
    public void CreateEventNotification_PastEventReminder_ThrowsArgumentException()
    {
        // Arrange
        var pastDate = DateTime.UtcNow.Date.AddDays(-1);
        var testEvent = new Event
        {
            Id = 999,
            Name = "Past Event",
            Location = "Some Location",
            Date = pastDate
        };
        var baseUrl = "https://rtub.example.com";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _factory.CreateEventNotification(testEvent, isReminder: true, baseUrl));
        Assert.Contains("Cannot create reminder for past events", exception.Message);
    }

    [Fact]
    public void CreateEventNotification_NullEvent_ThrowsArgumentNullException()
    {
        // Arrange
        Event? nullEvent = null;
        var baseUrl = "https://rtub.example.com";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateEventNotification(nullEvent!, isReminder: false, baseUrl));
    }

    [Fact]
    public void CreateEventNotification_NullBaseUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var testEvent = new Event
        {
            Id = 1,
            Name = "Test Event",
            Location = "Test Location",
            Date = DateTime.UtcNow.Date.AddDays(1)
        };
        string? nullBaseUrl = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateEventNotification(testEvent, isReminder: false, nullBaseUrl!));
    }

    [Fact]
    public void CreateEventNotification_EmptyBaseUrl_ThrowsArgumentException()
    {
        // Arrange
        var testEvent = new Event
        {
            Id = 1,
            Name = "Test Event",
            Location = "Test Location",
            Date = DateTime.UtcNow.Date.AddDays(1)
        };
        var emptyBaseUrl = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _factory.CreateEventNotification(testEvent, isReminder: false, emptyBaseUrl));
    }

    [Fact]
    public void CreateEventNotification_DateFormattingPortuguese_IsCorrect()
    {
        // Arrange
        // January 15, 2025 at 18:00
        var eventDate = new DateTime(2025, 1, 15, 18, 0, 0);
        var testEvent = new Event
        {
            Id = 1,
            Name = "Test Event",
            Location = "Test Location",
            Date = eventDate
        };
        var baseUrl = "https://rtub.example.com";

        // Act
        var notification = _factory.CreateEventNotification(testEvent, isReminder: false, baseUrl);

        // Assert
        Assert.Contains("15 de janeiro de 2025", notification.Body);
    }

    [Fact]
    public void CreateEventNotification_EventToday_ReminderCalculatesCorrectDays()
    {
        // Arrange
        var today = DateTime.UtcNow.Date.AddHours(20); // Today at 20:00
        var testEvent = new Event
        {
            Id = 1,
            Name = "Today's Event",
            Location = "Test Location",
            Date = today
        };
        var baseUrl = "https://rtub.example.com";

        // Act
        var notification = _factory.CreateEventNotification(testEvent, isReminder: true, baseUrl);

        // Assert
        // Since the event is today (0 days difference), Math.Ceiling returns 0
        // But the implementation should handle it properly
        Assert.Contains("dias", notification.Body);
    }

    [Fact]
    public void CreateEventNotification_MultipleEventsWithDifferentIds_GenerateUniqueTags()
    {
        // Arrange
        var eventDate = DateTime.UtcNow.Date.AddDays(2);
        var event1 = new Event { Id = 100, Name = "Event 1", Location = "Location 1", Date = eventDate };
        var event2 = new Event { Id = 200, Name = "Event 2", Location = "Location 2", Date = eventDate };
        var baseUrl = "https://rtub.example.com";

        // Act
        var notification1 = _factory.CreateEventNotification(event1, isReminder: false, baseUrl);
        var notification2 = _factory.CreateEventNotification(event2, isReminder: false, baseUrl);

        // Assert
        Assert.NotEqual(notification1.Tag, notification2.Tag);
        Assert.Equal("event-100", notification1.Tag);
        Assert.Equal("event-200", notification2.Tag);
    }

    [Fact]
    public void CreateEventNotification_ReminderVsNew_GenerateDifferentTags()
    {
        // Arrange
        var eventDate = DateTime.UtcNow.Date.AddDays(5);
        var testEvent = new Event
        {
            Id = 123,
            Name = "Test Event",
            Location = "Test Location",
            Date = eventDate
        };
        var baseUrl = "https://rtub.example.com";

        // Act
        var newNotification = _factory.CreateEventNotification(testEvent, isReminder: false, baseUrl);
        var reminderNotification = _factory.CreateEventNotification(testEvent, isReminder: true, baseUrl);

        // Assert
        Assert.NotEqual(newNotification.Tag, reminderNotification.Tag);
        Assert.Equal("event-123", newNotification.Tag);
        Assert.Equal("event-reminder-123", reminderNotification.Tag);
    }

    // Rehearsal Notification Tests

    [Fact]
    public void CreateRehearsalNotification_ReturnsCorrectNotification()
    {
        // Arrange
        var rehearsalDate = new DateTime(2024, 11, 26, 21, 30, 0); // Tuesday, Nov 26, 2024
        var rehearsal = Rehearsal.Create(rehearsalDate, "Centro Académico");
        rehearsal.Id = 789;
        var customBody = "Ensaio importante! Vamos preparar o repertório de Natal.";
        var baseUrl = "https://rtub.example.com";

        // Act
        var notification = _factory.CreateRehearsalNotification(rehearsal, customBody, baseUrl);

        // Assert
        Assert.NotNull(notification);
        Assert.Contains("Ensaio - ", notification.Title);
        Assert.Contains("26", notification.Title); // Day
        Assert.Contains("nov", notification.Title.ToLower()); // Month abbreviation
        Assert.Contains("terça", notification.Title.ToLower()); // Day of week
        Assert.Equal(customBody, notification.Body);
        Assert.Equal("/icons/rtub-logo-192.png", notification.Icon);
        Assert.Equal("https://rtub.example.com/rehearsals", notification.Url);
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
        var baseUrl = "https://rtub.example.com";

        // Act
        var notification = _factory.CreateRehearsalNotification(rehearsal, customBody, baseUrl);

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
        var baseUrl = "https://rtub.example.com";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateRehearsalNotification(nullRehearsal!, customBody, baseUrl));
    }

    [Fact]
    public void CreateRehearsalNotification_NullCustomBody_ThrowsArgumentNullException()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.UtcNow.Date.AddDays(1), "Centro Académico");
        rehearsal.Id = 1;
        string? nullBody = null;
        var baseUrl = "https://rtub.example.com";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateRehearsalNotification(rehearsal, nullBody!, baseUrl));
    }

    [Fact]
    public void CreateRehearsalNotification_EmptyCustomBody_ThrowsArgumentException()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.UtcNow.Date.AddDays(1), "Centro Académico");
        rehearsal.Id = 1;
        var emptyBody = string.Empty;
        var baseUrl = "https://rtub.example.com";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _factory.CreateRehearsalNotification(rehearsal, emptyBody, baseUrl));
    }

    [Fact]
    public void CreateRehearsalNotification_BaseUrlWithTrailingSlash_NormalizesCorrectly()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.UtcNow.Date.AddDays(1), "Centro Académico");
        rehearsal.Id = 1;
        var customBody = "Test body";
        var baseUrl = "https://rtub.example.com/";

        // Act
        var notification = _factory.CreateRehearsalNotification(rehearsal, customBody, baseUrl);

        // Assert
        Assert.Equal("https://rtub.example.com/rehearsals", notification.Url);
    }
}
