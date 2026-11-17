// Rehearsal primary and other instrument counts
using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public partial class RehearsalTests
{
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
