using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Event entity
/// Tests domain logic and entity behavior
/// </summary>
public class EventTests
{
    [Fact]
    public void Create_WithValidData_CreatesEvent()
    {
        // Arrange
        var name = "Test Event";
        var date = DateTime.Now.AddDays(7);
        var location = "Test Location";
        var type = EventType.Festival;

        // Act
        var result = Event.Create(name, date, location, type);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Date.Should().Be(date);
        result.Location.Should().Be(location);
        result.Type.Should().Be(type);
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var name = "";
        var date = DateTime.Now.AddDays(7);
        var location = "Test Location";
        var type = EventType.Festival;

        // Act & Assert
        var act = () => Event.Create(name, date, location, type);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nome do evento*");
    }

    [Fact]
    public void UpdateDetails_WithValidData_UpdatesEvent()
    {
        // Arrange
        var event1 = Event.Create("Original Name", DateTime.Now, "Original Location", EventType.Festival);
        var newName = "Updated Name";
        var newDate = DateTime.Now.AddDays(14);
        var newLocation = "Updated Location";
        var newDescription = "Updated Description";

        // Act
        event1.UpdateDetails(newName, newDate, newLocation, newDescription, EventType.Atuacao);

        // Assert
        event1.Name.Should().Be(newName);
        event1.Date.Should().Be(newDate);
        event1.Location.Should().Be(newLocation);
        event1.Description.Should().Be(newDescription);
        event1.Type.Should().Be(EventType.Atuacao);
    }

    [Fact]
    public void Cancel_WithValidReason_CancelsEvent()
    {
        // Arrange
        var event1 = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        var reason = "Mau tempo previsto";

        // Act
        event1.Cancel(reason);

        // Assert
        event1.IsCancelled.Should().BeTrue();
        event1.CancellationReason.Should().Be(reason);
    }

    [Fact]
    public void Cancel_WithEmptyReason_ThrowsArgumentException()
    {
        // Arrange
        var event1 = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        var reason = "";

        // Act & Assert
        var act = () => event1.Cancel(reason);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*motivo de cancelamento*");
    }

    [Fact]
    public void Uncancel_WithCancelledEvent_UncancelsEvent()
    {
        // Arrange
        var event1 = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        event1.Cancel("Mau tempo previsto");

        // Act
        event1.Uncancel();

        // Assert
        event1.IsCancelled.Should().BeFalse();
        event1.CancellationReason.Should().BeNull();
    }

    [Fact]
    public void GetPrimaryInstrumentCounts_WithEnrollments_ReturnsCorrectCounts()
    {
        // Arrange
        var event1 = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        var user1 = "user-1";
        var user2 = "user-2";
        var user3 = "user-3";

        // Add enrollments (using eventId = 0 since we're testing the method directly)
        var enrollment1 = new Enrollment { UserId = user1, EventId = 0, Instrument = InstrumentType.Guitarra, WillAttend = true };
        var enrollment2 = new Enrollment { UserId = user2, EventId = 0, Instrument = InstrumentType.Guitarra, WillAttend = true };
        var enrollment3 = new Enrollment { UserId = user3, EventId = 0, Instrument = InstrumentType.Bandolim, WillAttend = true };

        event1.Enrollments.Add(enrollment1);
        event1.Enrollments.Add(enrollment2);
        event1.Enrollments.Add(enrollment3);

        // Setup member instruments - user1 and user2 have Guitarra as primary, user3 has Cavaquinho
        var memberInstruments = new Dictionary<string, List<MemberInstrument>>
        {
            [user1] = new List<MemberInstrument> { MemberInstrument.Create(user1, InstrumentType.Guitarra, isPrimary: true) },
            [user2] = new List<MemberInstrument> { MemberInstrument.Create(user2, InstrumentType.Guitarra, isPrimary: true) },
            [user3] = new List<MemberInstrument> { MemberInstrument.Create(user3, InstrumentType.Cavaquinho, isPrimary: true) }
        };

        // Act
        var result = event1.GetPrimaryInstrumentCounts(memberInstruments);

        // Assert - should count all selected instruments regardless of primary status
        result.Should().ContainKey(InstrumentType.Guitarra);
        result[InstrumentType.Guitarra].Should().Be(2);
        result.Should().ContainKey(InstrumentType.Bandolim); // user3 is playing Bandolim (non-primary)
        result[InstrumentType.Bandolim].Should().Be(1);
    }

    [Fact]
    public void GetOtherInstrumentCounts_OnlyCountsFromOtherInstrumentsField()
    {
        // Arrange
        var event1 = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        var user1 = "user-1";
        var user2 = "user-2";
        var user3 = "user-3";

        // Add enrollments - selected instruments should NOT be counted in "other"
        var enrollment1 = new Enrollment { UserId = user1, EventId = 0, Instrument = InstrumentType.Guitarra, WillAttend = true, OtherInstruments = "Bandolim, Cavaquinho" };
        var enrollment2 = new Enrollment { UserId = user2, EventId = 0, Instrument = InstrumentType.Bandolim, WillAttend = true, OtherInstruments = "Guitarra" };
        var enrollment3 = new Enrollment { UserId = user3, EventId = 0, Instrument = InstrumentType.Cavaquinho, WillAttend = true, OtherInstruments = null };

        event1.Enrollments.Add(enrollment1);
        event1.Enrollments.Add(enrollment2);
        event1.Enrollments.Add(enrollment3);

        // Setup member instruments
        var memberInstruments = new Dictionary<string, List<MemberInstrument>>
        {
            [user1] = new List<MemberInstrument> { MemberInstrument.Create(user1, InstrumentType.Guitarra, isPrimary: true) },
            [user2] = new List<MemberInstrument>
            {
                MemberInstrument.Create(user2, InstrumentType.Guitarra, isPrimary: true),
                MemberInstrument.Create(user2, InstrumentType.Bandolim, isPrimary: false)
            }
        };

        // Act
        var result = event1.GetOtherInstrumentCounts(memberInstruments);

        // Assert - only instruments from OtherInstruments field are counted
        result.Should().ContainKey(InstrumentType.Bandolim);
        result[InstrumentType.Bandolim].Should().Be(1); // from user1's OtherInstruments
        result.Should().ContainKey(InstrumentType.Cavaquinho);
        result[InstrumentType.Cavaquinho].Should().Be(1); // from user1's OtherInstruments
        result.Should().ContainKey(InstrumentType.Guitarra);
        result[InstrumentType.Guitarra].Should().Be(1); // from user2's OtherInstruments
        // Selected instruments should NOT be counted in "other"
    }

    [Fact]
    public void GetPrimaryInstrumentCounts_WithNotAttendingMembers_ExcludesThem()
    {
        // Arrange
        var event1 = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        var user1 = "user-1";

        // Add enrollment with WillAttend = false
        var enrollment1 = new Enrollment { UserId = user1, EventId = 0, Instrument = InstrumentType.Guitarra, WillAttend = false };
        event1.Enrollments.Add(enrollment1);

        var memberInstruments = new Dictionary<string, List<MemberInstrument>>
        {
            [user1] = new List<MemberInstrument> { MemberInstrument.Create(user1, InstrumentType.Guitarra, isPrimary: true) }
        };

        // Act
        var result = event1.GetPrimaryInstrumentCounts(memberInstruments);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetOtherInstrumentCounts_WithOtherInstrumentsField_ParsesAndCountsCorrectly()
    {
        // Arrange
        var event1 = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        var user1 = "user-1";
        var user2 = "user-2";

        // user1 is playing Bandolim (primary), has "Guitarra, Cavaquinho" in OtherInstruments
        var enrollment1 = new Enrollment
        {
            UserId = user1,
            EventId = 0,
            Instrument = InstrumentType.Bandolim,
            OtherInstruments = "Guitarra, Cavaquinho",
            WillAttend = true
        };

        // user2 is playing Guitarra (not primary), has "Acordeão" in OtherInstruments
        var enrollment2 = new Enrollment
        {
            UserId = user2,
            EventId = 0,
            Instrument = InstrumentType.Guitarra,
            OtherInstruments = "Acordeão",
            WillAttend = true
        };

        event1.Enrollments.Add(enrollment1);
        event1.Enrollments.Add(enrollment2);

        // Setup member instruments - user1 has Bandolim as primary, user2 has Cavaquinho as primary
        var memberInstruments = new Dictionary<string, List<MemberInstrument>>
        {
            [user1] = new List<MemberInstrument> { MemberInstrument.Create(user1, InstrumentType.Bandolim, isPrimary: true) },
            [user2] = new List<MemberInstrument> { MemberInstrument.Create(user2, InstrumentType.Cavaquinho, isPrimary: true) }
        };

        // Act
        var result = event1.GetOtherInstrumentCounts(memberInstruments);

        // Assert
        // ONLY counts from OtherInstruments field (selected instruments counted in GetPrimaryInstrumentCounts)
        result.Should().ContainKey(InstrumentType.Guitarra);
        result[InstrumentType.Guitarra].Should().Be(1); // from user1's OtherInstruments field
        result.Should().ContainKey(InstrumentType.Cavaquinho);
        result[InstrumentType.Cavaquinho].Should().Be(1); // from user1's OtherInstruments field
        result.Should().ContainKey(InstrumentType.Acordeao);
        result[InstrumentType.Acordeao].Should().Be(1); // from user2's OtherInstruments field
        // Selected instruments should NOT be counted in "other"
    }

    [Fact]
    public void GetOtherInstrumentCounts_WithEmptyOtherInstruments_ReturnsEmpty()
    {
        // Arrange
        var event1 = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        var user1 = "user-1";

        var enrollment1 = new Enrollment
        {
            UserId = user1,
            EventId = 0,
            Instrument = InstrumentType.Guitarra,
            OtherInstruments = "",
            WillAttend = true
        };

        event1.Enrollments.Add(enrollment1);

        var memberInstruments = new Dictionary<string, List<MemberInstrument>>
        {
            [user1] = new List<MemberInstrument> { MemberInstrument.Create(user1, InstrumentType.Bandolim, isPrimary: true) }
        };

        // Act
        var result = event1.GetOtherInstrumentCounts(memberInstruments);

        // Assert - empty OtherInstruments should result in empty counts
        result.Should().BeEmpty("selected instrument is not counted in 'other', only OtherInstruments field is parsed");
    }
}
