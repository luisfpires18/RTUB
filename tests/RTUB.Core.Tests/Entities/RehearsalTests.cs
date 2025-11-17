using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Rehearsal entity
/// Tests domain logic and entity behavior
/// </summary>
public class RehearsalTests
{
    [Fact]
    public void Create_WithValidData_CreatesRehearsal()
    {
        // Arrange
        var date = DateTime.Now.AddDays(7);
        var location = "Centro Académico";
        var theme = "Fado practice";

        // Act
        var result = Rehearsal.Create(date, location, theme);

        // Assert
        result.Should().NotBeNull();
        result.Date.Should().Be(date.Date);
        result.Location.Should().Be(location);
        result.Theme.Should().Be(theme);
        result.IsCanceled.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyLocation_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.Now.AddDays(7);
        var location = "";

        // Act & Assert
        var act = () => Rehearsal.Create(date, location);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Location*");
    }

    [Fact]
    public void Create_WithNullLocation_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.Now.AddDays(7);
        string? location = null;

        // Act & Assert
        var act = () => Rehearsal.Create(date, location!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Location*");
    }

    [Fact]
    public void Create_NormalizesDateToDateOnly()
    {
        // Arrange
        var dateTime = new DateTime(2025, 10, 31, 14, 30, 45);
        var location = "Test Location";

        // Act
        var result = Rehearsal.Create(dateTime, location);

        // Assert
        result.Date.Should().Be(dateTime.Date);
        result.Date.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Create_WithoutTheme_SetsThemeToNull()
    {
        // Arrange
        var date = DateTime.Now.AddDays(7);
        var location = "Test Location";

        // Act
        var result = Rehearsal.Create(date, location);

        // Assert
        result.Theme.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_WithValidData_UpdatesRehearsal()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Original Location");
        var newLocation = "New Location";
        var newTheme = "New Theme";
        var newNotes = "New Notes";

        // Act
        rehearsal.UpdateDetails(newLocation, newTheme, newNotes);

        // Assert
        rehearsal.Location.Should().Be(newLocation);
        rehearsal.Theme.Should().Be(newTheme);
        rehearsal.Notes.Should().Be(newNotes);
    }

    [Fact]
    public void UpdateDetails_WithEmptyLocation_ThrowsArgumentException()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Original Location");

        // Act & Assert
        var act = () => rehearsal.UpdateDetails("", "Theme", "Notes");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Location*");
    }

    [Fact]
    public void Cancel_SetsCanceledToTrue()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");

        // Act
        rehearsal.Cancel();

        // Assert
        rehearsal.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public void Reactivate_SetsCanceledToFalse()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        rehearsal.Cancel();

        // Act
        rehearsal.Reactivate();

        // Assert
        rehearsal.IsCanceled.Should().BeFalse();
    }

    [Fact]
    public void DefaultStartTime_Is21Hours()
    {
        // Arrange & Act
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");

        // Assert
        rehearsal.StartTime.Should().Be(new TimeSpan(21, 30, 0));
    }

    [Fact]
    public void DefaultEndTime_IsMidnight()
    {
        // Arrange & Act
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");

        // Assert
        rehearsal.EndTime.Should().Be(new TimeSpan(0, 0, 0));
    }

    [Fact]
    public void Attendances_InitializedAsEmptyCollection()
    {
        // Arrange & Act
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");

        // Assert
        rehearsal.Attendances.Should().NotBeNull();
        rehearsal.Attendances.Should().BeEmpty();
    }

    [Fact]
    public void GetPrimaryInstrumentCounts_WithAttendances_ReturnsCorrectCounts()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        var user1 = "user-1";
        var user2 = "user-2";
        var user3 = "user-3";

        // Add attendances using direct initialization since rehearsalId is not set
        rehearsal.Attendances.Add(new RehearsalAttendance 
        { 
            RehearsalId = 1, 
            UserId = user1, 
            Instrument = RTUB.Core.Enums.InstrumentType.Guitarra, 
            WillAttend = true 
        });
        rehearsal.Attendances.Add(new RehearsalAttendance 
        { 
            RehearsalId = 1, 
            UserId = user2, 
            Instrument = RTUB.Core.Enums.InstrumentType.Guitarra, 
            WillAttend = true 
        });
        rehearsal.Attendances.Add(new RehearsalAttendance 
        { 
            RehearsalId = 1, 
            UserId = user3, 
            Instrument = RTUB.Core.Enums.InstrumentType.Bandolim, 
            WillAttend = true 
        });

        // Setup member instruments
        var memberInstruments = new Dictionary<string, List<MemberInstrument>>
        {
            [user1] = new List<MemberInstrument> { MemberInstrument.Create(user1, RTUB.Core.Enums.InstrumentType.Guitarra, isPrimary: true) },
            [user2] = new List<MemberInstrument> { MemberInstrument.Create(user2, RTUB.Core.Enums.InstrumentType.Guitarra, isPrimary: true) },
            [user3] = new List<MemberInstrument> { MemberInstrument.Create(user3, RTUB.Core.Enums.InstrumentType.Cavaquinho, isPrimary: true) }
        };

        // Act
        var result = rehearsal.GetPrimaryInstrumentCounts(memberInstruments);

        // Assert - should count all selected instruments regardless of primary status
        result.Should().ContainKey(RTUB.Core.Enums.InstrumentType.Guitarra);
        result[RTUB.Core.Enums.InstrumentType.Guitarra].Should().Be(2);
        result.Should().ContainKey(RTUB.Core.Enums.InstrumentType.Bandolim); // user3 is playing Bandolim (non-primary)
        result[RTUB.Core.Enums.InstrumentType.Bandolim].Should().Be(1);
    }

    [Fact]
    public void GetOtherInstrumentCounts_OnlyCountsFromOtherInstrumentsField()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        var user1 = "user-1";
        var user2 = "user-2";

        // Add attendances - selected instruments should NOT be counted in "other"
        rehearsal.Attendances.Add(new RehearsalAttendance 
        { 
            RehearsalId = 1, 
            UserId = user1, 
            Instrument = RTUB.Core.Enums.InstrumentType.Bandolim, 
            OtherInstruments = "Guitarra, Cavaquinho",
            WillAttend = true 
        });
        rehearsal.Attendances.Add(new RehearsalAttendance 
        { 
            RehearsalId = 1, 
            UserId = user2, 
            Instrument = RTUB.Core.Enums.InstrumentType.Cavaquinho, 
            OtherInstruments = "Guitarra",
            WillAttend = true 
        });

        // Setup member instruments
        var memberInstruments = new Dictionary<string, List<MemberInstrument>>
        {
            [user1] = new List<MemberInstrument> 
            { 
                MemberInstrument.Create(user1, RTUB.Core.Enums.InstrumentType.Guitarra, isPrimary: true),
                MemberInstrument.Create(user1, RTUB.Core.Enums.InstrumentType.Bandolim, isPrimary: false)
            }
        };

        // Act
        var result = rehearsal.GetOtherInstrumentCounts(memberInstruments);

        // Assert - only instruments from OtherInstruments field are counted
        result.Should().ContainKey(RTUB.Core.Enums.InstrumentType.Guitarra);
        result[RTUB.Core.Enums.InstrumentType.Guitarra].Should().Be(2); // from both users' OtherInstruments
        result.Should().ContainKey(RTUB.Core.Enums.InstrumentType.Cavaquinho);
        result[RTUB.Core.Enums.InstrumentType.Cavaquinho].Should().Be(1); // from user1's OtherInstruments
        // Selected instruments (Bandolim, Cavaquinho) should NOT be counted in "other"
    }

    [Fact]
    public void GetOtherInstrumentCounts_WithOtherInstrumentsField_ParsesAndCountsCorrectly()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        var user1 = "user-1";
        var user2 = "user-2";

        // user1 is playing Bandolim (primary), has "Guitarra, Cavaquinho" in OtherInstruments
        rehearsal.Attendances.Add(new RehearsalAttendance 
        { 
            RehearsalId = 1, 
            UserId = user1, 
            Instrument = RTUB.Core.Enums.InstrumentType.Bandolim, 
            OtherInstruments = "Guitarra, Cavaquinho",
            WillAttend = true 
        });
        
        // user2 is playing Guitarra (not primary), has "Acordeão" in OtherInstruments
        rehearsal.Attendances.Add(new RehearsalAttendance 
        { 
            RehearsalId = 1, 
            UserId = user2, 
            Instrument = RTUB.Core.Enums.InstrumentType.Guitarra, 
            OtherInstruments = "Acordeão",
            WillAttend = true 
        });

        // Setup member instruments
        var memberInstruments = new Dictionary<string, List<MemberInstrument>>
        {
            [user1] = new List<MemberInstrument> { MemberInstrument.Create(user1, RTUB.Core.Enums.InstrumentType.Bandolim, isPrimary: true) },
            [user2] = new List<MemberInstrument> { MemberInstrument.Create(user2, RTUB.Core.Enums.InstrumentType.Cavaquinho, isPrimary: true) }
        };

        // Act
        var result = rehearsal.GetOtherInstrumentCounts(memberInstruments);

        // Assert - only count from OtherInstruments field:
        result.Should().ContainKey(RTUB.Core.Enums.InstrumentType.Guitarra);
        result[RTUB.Core.Enums.InstrumentType.Guitarra].Should().Be(1); // user1's OtherInstruments only
        result.Should().ContainKey(RTUB.Core.Enums.InstrumentType.Cavaquinho);
        result[RTUB.Core.Enums.InstrumentType.Cavaquinho].Should().Be(1); // user1's OtherInstruments
        result.Should().ContainKey(RTUB.Core.Enums.InstrumentType.Acordeao);
        result[RTUB.Core.Enums.InstrumentType.Acordeao].Should().Be(1); // user2's OtherInstruments
    }

    [Fact]
    public void GetOtherInstrumentCounts_WithEmptyOtherInstruments_ReturnsEmpty()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        var user1 = "user-1";

        rehearsal.Attendances.Add(new RehearsalAttendance 
        { 
            RehearsalId = 1, 
            UserId = user1, 
            Instrument = RTUB.Core.Enums.InstrumentType.Guitarra, 
            OtherInstruments = "",
            WillAttend = true 
        });

        var memberInstruments = new Dictionary<string, List<MemberInstrument>>
        {
            [user1] = new List<MemberInstrument> { MemberInstrument.Create(user1, RTUB.Core.Enums.InstrumentType.Bandolim, isPrimary: true) }
        };

        // Act
        var result = rehearsal.GetOtherInstrumentCounts(memberInstruments);

        // Assert - empty OtherInstruments should result in empty counts
        result.Should().BeEmpty("selected instrument is not counted in 'other', only OtherInstruments field is parsed");
    }
}
