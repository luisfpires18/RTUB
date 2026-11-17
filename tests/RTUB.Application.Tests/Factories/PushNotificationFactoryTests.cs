using RTUB.Application.Factories;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Factories;

public partial class PushNotificationFactoryTests
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

        // Act
        var notification = _factory.CreateEventNotification(testEvent, isReminder: false);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal("Atuação de Natal", notification.Title);
        Assert.Equal("Novo evento do tipo \"Festival\" - Atuação de Natal. Confirma a tua presença.", notification.Body);
        Assert.Equal("/icons/rtub-logo-192.png", notification.Icon);
        Assert.Equal("/events/123", notification.Url);
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

        // Act
        var notification = _factory.CreateEventNotification(testEvent, isReminder: true);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal("Lembrete: Ensaio Geral", notification.Title);
        Assert.Contains("A atuação é em 3 dias", notification.Body);
        Assert.Contains("no Sala de Ensaios", notification.Body);
        Assert.Contains("Não te esqueças de confirmar a tua presença!", notification.Body);
        Assert.Equal("/icons/rtub-logo-192.png", notification.Icon);
        Assert.Equal("/events/456", notification.Url);
        Assert.StartsWith("event-reminder-456-", notification.Tag);
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

        // Act
        var notification = _factory.CreateEventNotification(testEvent, isReminder: true);

        // Assert
        Assert.NotNull(notification);
        Assert.Contains("A atuação é em 1 dia", notification.Body);
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

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _factory.CreateEventNotification(testEvent, isReminder: true));
        Assert.Contains("Cannot create reminder for past events", exception.Message);
    }

    [Fact]
    public void CreateEventNotification_NullEvent_ThrowsArgumentNullException()
    {
        // Arrange
        Event? nullEvent = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateEventNotification(nullEvent!, isReminder: false));
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

        // Act
        var notification = _factory.CreateEventNotification(testEvent, isReminder: false);

        // Assert
        Assert.Contains("Novo evento do tipo", notification.Body);
        Assert.Contains("Test Event", notification.Body);
    }

    [Fact]
    public void CreatePendingRehearsalApprovalsReminderNotification_SinglePending_ReusesSingularBody()
    {
        // Arrange

        // Act
        var notification = _factory.CreatePendingRehearsalApprovalsReminderNotification(1);

        // Assert
        Assert.Equal("Lembrete: Ensaios Pendentes", notification.Title);
        Assert.Equal("Existe 1 ensaio passado com presenças pendentes para aprovar.", notification.Body);
        Assert.Equal("/icons/rtub-logo-192.png", notification.Icon);
        Assert.Equal("/rehearsals", notification.Url);
        Assert.Equal("pending-rehearsal-approvals-reminder", notification.Tag);
    }

    [Fact]
    public void CreatePendingRehearsalApprovalsReminderNotification_MultiplePending_UsesPluralBody()
    {
        // Arrange

        // Act
        var notification = _factory.CreatePendingRehearsalApprovalsReminderNotification(3);

        // Assert
        Assert.Equal("Existem 3 ensaios passados com presenças pendentes para aprovar.", notification.Body);
        Assert.Equal("/rehearsals", notification.Url);
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

        // Act
        var notification = _factory.CreateEventNotification(testEvent, isReminder: true);

        // Assert
        // Since the event is today (0 days difference), it should say "hoje"
        Assert.Contains("A atuação é hoje", notification.Body);
        Assert.DoesNotContain("dias", notification.Body);
    }

    [Fact]
    public void CreateEventNotification_MultipleEventsWithDifferentIds_GenerateUniqueTags()
    {
        // Arrange
        var eventDate = DateTime.UtcNow.Date.AddDays(2);
        var event1 = new Event { Id = 100, Name = "Event 1", Location = "Location 1", Date = eventDate };
        var event2 = new Event { Id = 200, Name = "Event 2", Location = "Location 2", Date = eventDate };

        // Act
        var notification1 = _factory.CreateEventNotification(event1, isReminder: false);
        var notification2 = _factory.CreateEventNotification(event2, isReminder: false);

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

        // Act
        var newNotification = _factory.CreateEventNotification(testEvent, isReminder: false);
        var reminderNotification = _factory.CreateEventNotification(testEvent, isReminder: true);

        // Assert
        Assert.NotEqual(newNotification.Tag, reminderNotification.Tag);
        Assert.Equal("event-123", newNotification.Tag);
        Assert.StartsWith("event-reminder-123-", reminderNotification.Tag);
    }
}
