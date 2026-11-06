using RTUB.Application.Interfaces;
using RTUB.Application.Factories;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
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

    [Fact]
    public void LogisticsNotifications_OpenTheCardOnItsBoard_WithTitleAndBoardOnly()
    {
        // Task 069: never the card's details; the link opens the card (/logistics/{board}?card={id}).
        foreach (var kind in Enum.GetValues<LogisticsNotice>())
        {
            var n = _factory.CreateLogisticsNotification(kind, 3, 41, "Comprar cordas", "FITAB 2027", "Zé", new DateTime(2027, 3, 1));
            Assert.Equal("/logistics/3?card=41", n.Url);
            Assert.Equal("logistics-card-41", n.Tag);
            Assert.Contains("«Comprar cordas» (FITAB 2027)", n.Body);
        }

        Assert.Equal("«Comprar cordas» (FITAB 2027) · prazo 01/03",
            _factory.CreateLogisticsNotification(LogisticsNotice.Reminder, 3, 41, "Comprar cordas", "FITAB 2027", dueDate: new DateTime(2027, 3, 1)).Body);
        Assert.Contains("Zé mencionou-te", _factory.CreateLogisticsNotification(LogisticsNotice.Mention, 3, 41, "x", "y", "Zé").Body);
        Assert.EndsWith("…» (y).", _factory.CreateLogisticsNotification(LogisticsNotice.Owner, 3, 41, new string('a', 200), "y").Body);
    }
}
