using FluentAssertions;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Extensions;

// ApplicationUserExtensionsTests: Effective Category tests.
public partial class ApplicationUserExtensionsTests
{
    [Fact]
    public void EffectiveCategory_Enrollment_WithSnapshot_PrefersSnapshotOverCurrent()
    {
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        var enrollment = new Enrollment
        {
            UserId = user.Id,
            EventId = 1,
            User = user,
            CategoryAtEvent = MemberCategory.Leitao
        };

        enrollment.EffectiveCategory().Should().Be(MemberCategory.Leitao);
    }

    [Fact]
    public void EffectiveCategory_Enrollment_NoSnapshot_FallsBackToUserPrimary()
    {
        var user = CreateUserWithCategories(MemberCategory.Caloiro);
        var enrollment = new Enrollment
        {
            UserId = user.Id,
            EventId = 1,
            User = user,
            CategoryAtEvent = null
        };

        enrollment.EffectiveCategory().Should().Be(MemberCategory.Caloiro);
    }

    [Fact]
    public void EffectiveCategory_Enrollment_NoSnapshotAndNoUser_ReturnsNull()
    {
        var enrollment = new Enrollment
        {
            UserId = "x",
            EventId = 1,
            User = null,
            CategoryAtEvent = null
        };

        enrollment.EffectiveCategory().Should().BeNull();
    }

    [Fact]
    public void EffectiveCategory_RehearsalAttendance_WithSnapshot_PrefersSnapshot()
    {
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        var attendance = new RehearsalAttendance
        {
            RehearsalId = 1,
            UserId = user.Id,
            User = user,
            CategoryAtRehearsal = MemberCategory.Leitao
        };

        attendance.EffectiveCategory().Should().Be(MemberCategory.Leitao);
    }

    [Fact]
    public void EffectiveCategory_RehearsalAttendance_NoSnapshot_FallsBackToUserPrimary()
    {
        var user = CreateUserWithCategories(MemberCategory.Veterano, MemberCategory.Tuno);
        var attendance = new RehearsalAttendance
        {
            RehearsalId = 1,
            UserId = user.Id,
            User = user,
            CategoryAtRehearsal = null
        };

        attendance.EffectiveCategory().Should().Be(MemberCategory.Veterano);
    }

    [Fact]
    public void EffectiveCategory_RehearsalAttendance_NoSnapshotAndNoUser_ReturnsNull()
    {
        var attendance = new RehearsalAttendance
        {
            RehearsalId = 1,
            UserId = "x",
            User = null,
            CategoryAtRehearsal = null
        };

        attendance.EffectiveCategory().Should().BeNull();
    }
}
