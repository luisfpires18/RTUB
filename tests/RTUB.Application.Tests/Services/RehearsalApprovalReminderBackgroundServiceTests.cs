using System;
using System.Collections.Generic;
using System.Linq;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Application.Tests.Services;

public class RehearsalApprovalReminderBackgroundServiceTests
{
    [Fact]
    public void GetPendingRehearsalIdsQuery_WhenPendingPastRehearsalsExist_ReturnsDistinctCount()
    {
        // Arrange
        var today = new DateTime(2025, 1, 10);
        var rehearsalOne = new Rehearsal { Id = 1, Date = new DateTime(2025, 1, 5), IsCanceled = false };
        var rehearsalTwo = new Rehearsal { Id = 2, Date = new DateTime(2025, 1, 6), IsCanceled = false };
        var rehearsalFuture = new Rehearsal { Id = 3, Date = new DateTime(2025, 1, 12), IsCanceled = false };
        var rehearsalCanceled = new Rehearsal { Id = 4, Date = new DateTime(2025, 1, 4), IsCanceled = true };

        var attendances = new List<RehearsalAttendance>
        {
            new RehearsalAttendance
            {
                RehearsalId = rehearsalOne.Id,
                Rehearsal = rehearsalOne,
                UserId = "user-1",
                WillAttend = true,
                Attended = false
            },
            new RehearsalAttendance
            {
                RehearsalId = rehearsalOne.Id,
                Rehearsal = rehearsalOne,
                UserId = "user-2",
                WillAttend = true,
                Attended = false
            },
            new RehearsalAttendance
            {
                RehearsalId = rehearsalTwo.Id,
                Rehearsal = rehearsalTwo,
                UserId = "user-3",
                WillAttend = true,
                Attended = true
            },
            new RehearsalAttendance
            {
                RehearsalId = rehearsalFuture.Id,
                Rehearsal = rehearsalFuture,
                UserId = "user-4",
                WillAttend = true,
                Attended = false
            },
            new RehearsalAttendance
            {
                RehearsalId = rehearsalCanceled.Id,
                Rehearsal = rehearsalCanceled,
                UserId = "user-5",
                WillAttend = true,
                Attended = false
            },
            new RehearsalAttendance
            {
                RehearsalId = rehearsalTwo.Id,
                Rehearsal = rehearsalTwo,
                UserId = "user-6",
                WillAttend = false,
                Attended = false
            }
        };

        // Act
        var count = RehearsalApprovalReminderBackgroundService
            .GetPendingRehearsalIdsQuery(attendances.AsQueryable(), today)
            .Count();

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public void GetPendingRehearsalIdsQuery_WhenNoPendingPastRehearsals_ReturnsZero()
    {
        // Arrange
        var today = new DateTime(2025, 1, 10);
        var rehearsal = new Rehearsal { Id = 1, Date = new DateTime(2025, 1, 5), IsCanceled = false };

        var attendances = new List<RehearsalAttendance>
        {
            new RehearsalAttendance
            {
                RehearsalId = rehearsal.Id,
                Rehearsal = rehearsal,
                UserId = "user-1",
                WillAttend = true,
                Attended = true
            }
        };

        // Act
        var count = RehearsalApprovalReminderBackgroundService
            .GetPendingRehearsalIdsQuery(attendances.AsQueryable(), today)
            .Count();

        // Assert
        Assert.Equal(0, count);
    }
}
