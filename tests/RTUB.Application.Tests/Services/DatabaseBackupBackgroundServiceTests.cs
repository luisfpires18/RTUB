using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for the DatabaseBackupBackgroundService time calculation logic.
/// </summary>
public class DatabaseBackupBackgroundServiceTests
{
    #region CalculateNextRunTime Tests

    [Fact]
    public void CalculateNextRunTime_SingleBackupTime_TimeNotPassedToday_ReturnsTodayAtBackupTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var futureTime = now.AddHours(2);
        var backupTimeString = futureTime.ToString("HH:mm");
        var options = CreateOptions(new List<string> { backupTimeString });
        var service = CreateService(options);

        // Act
        var result = InvokeCalculateNextRunTime(service);

        // Assert
        Assert.Equal(now.Date, result.Date);
        Assert.Equal(futureTime.Hour, result.Hour);
        Assert.Equal(futureTime.Minute, result.Minute);
        Assert.True(result > now, "Next run time should be in the future");
    }

    [Fact]
    public void CalculateNextRunTime_SingleBackupTime_TimeAlreadyPassedToday_ReturnsTomorrowAtBackupTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var pastTime = now.AddHours(-2);
        var backupTimeString = pastTime.ToString("HH:mm");
        var options = CreateOptions(new List<string> { backupTimeString });
        var service = CreateService(options);

        // Act
        var result = InvokeCalculateNextRunTime(service);

        // Assert
        Assert.Equal(now.Date.AddDays(1), result.Date);
        Assert.Equal(pastTime.Hour, result.Hour);
        Assert.Equal(pastTime.Minute, result.Minute);
        Assert.True(result > now, "Next run time should be in the future");
    }

    [Fact]
    public void CalculateNextRunTime_MultipleBackupTimes_NextOneIsToday_ReturnsTodayAtNextBackupTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var pastTime = now.AddHours(-2);
        var futureTime1 = now.AddHours(1);
        var futureTime2 = now.AddHours(3);
        
        var options = CreateOptions(new List<string> 
        { 
            pastTime.ToString("HH:mm"),
            futureTime1.ToString("HH:mm"),
            futureTime2.ToString("HH:mm")
        });
        var service = CreateService(options);

        // Act
        var result = InvokeCalculateNextRunTime(service);

        // Assert
        Assert.Equal(now.Date, result.Date);
        Assert.Equal(futureTime1.Hour, result.Hour);
        Assert.Equal(futureTime1.Minute, result.Minute);
        Assert.True(result > now, "Next run time should be in the future");
        Assert.True(result < futureTime2, "Should return the earliest future time");
    }

    [Fact]
    public void CalculateNextRunTime_MultipleBackupTimes_AllPassedToday_ReturnsTomorrowFirstTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var pastTime1 = now.AddHours(-5);
        var pastTime2 = now.AddHours(-3);
        var pastTime3 = now.AddHours(-1);
        
        var options = CreateOptions(new List<string> 
        { 
            pastTime2.ToString("HH:mm"),  // Middle time
            pastTime3.ToString("HH:mm"),  // Latest time
            pastTime1.ToString("HH:mm")   // Earliest time
        });
        var service = CreateService(options);

        // Act
        var result = InvokeCalculateNextRunTime(service);

        // Assert
        Assert.Equal(now.Date.AddDays(1), result.Date);
        // Should be the earliest time (pastTime1) scheduled for tomorrow
        Assert.Equal(pastTime1.Hour, result.Hour);
        Assert.Equal(pastTime1.Minute, result.Minute);
        Assert.True(result > now, "Next run time should be in the future");
    }

    [Fact]
    public void CalculateNextRunTime_EmptyBackupTimes_UsesDefault08_00()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var options = CreateOptions(new List<string>());
        var service = CreateService(options);

        // Act
        var result = InvokeCalculateNextRunTime(service);

        // Assert
        Assert.Equal(8, result.Hour);
        Assert.Equal(0, result.Minute);
        Assert.True(result > now, "Next run time should be in the future");
        
        // Verify it's today if before 08:00, tomorrow if after 08:00
        var expectedDate = now.Hour < 8 ? now.Date : now.Date.AddDays(1);
        Assert.Equal(expectedDate, result.Date);
    }

    [Fact]
    public void CalculateNextRunTime_InvalidTimeFormat_FallsBackToDefault08_00()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var options = CreateOptions(new List<string> { "invalid-time", "not-a-time", "25:99" });
        var service = CreateService(options);

        // Act
        var result = InvokeCalculateNextRunTime(service);

        // Assert
        Assert.Equal(8, result.Hour);
        Assert.Equal(0, result.Minute);
        Assert.True(result > now, "Next run time should be in the future");
    }

    [Fact]
    public void CalculateNextRunTime_MixedValidAndInvalidTimes_UsesOnlyValidTimes()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var futureTime = now.AddHours(2);
        var options = CreateOptions(new List<string> 
        { 
            "invalid-time",
            futureTime.ToString("HH:mm"),
            "not-a-time"
        });
        var service = CreateService(options);

        // Act
        var result = InvokeCalculateNextRunTime(service);

        // Assert
        Assert.Equal(now.Date, result.Date);
        Assert.Equal(futureTime.Hour, result.Hour);
        Assert.Equal(futureTime.Minute, result.Minute);
    }

    [Fact]
    public void CalculateNextRunTime_UnorderedBackupTimes_ReturnsEarliestFutureTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var futureTime1 = now.AddHours(3);
        var futureTime2 = now.AddHours(1);
        var futureTime3 = now.AddHours(2);
        
        // Provide times in non-chronological order
        var options = CreateOptions(new List<string> 
        { 
            futureTime1.ToString("HH:mm"),  // 3 hours from now
            futureTime2.ToString("HH:mm"),  // 1 hour from now (should be selected)
            futureTime3.ToString("HH:mm")   // 2 hours from now
        });
        var service = CreateService(options);

        // Act
        var result = InvokeCalculateNextRunTime(service);

        // Assert
        Assert.Equal(now.Date, result.Date);
        Assert.Equal(futureTime2.Hour, result.Hour);
        Assert.Equal(futureTime2.Minute, result.Minute);
    }

    [Fact]
    public void CalculateNextRunTime_DuplicateBackupTimes_HandlesCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var futureTime = now.AddHours(2);
        var timeString = futureTime.ToString("HH:mm");
        
        var options = CreateOptions(new List<string> 
        { 
            timeString,
            timeString,
            timeString
        });
        var service = CreateService(options);

        // Act
        var result = InvokeCalculateNextRunTime(service);

        // Assert
        Assert.Equal(now.Date, result.Date);
        Assert.Equal(futureTime.Hour, result.Hour);
        Assert.Equal(futureTime.Minute, result.Minute);
    }

    [Fact]
    public void CalculateNextRunTime_MidnightBackupTime_HandlesCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var options = CreateOptions(new List<string> { "00:00" });
        var service = CreateService(options);

        // Act
        var result = InvokeCalculateNextRunTime(service);

        // Assert
        Assert.Equal(0, result.Hour);
        Assert.Equal(0, result.Minute);
        
        // If current time is after midnight, should be tomorrow
        if (now.Hour > 0 || now.Minute > 0)
        {
            Assert.Equal(now.Date.AddDays(1), result.Date);
        }
        else
        {
            // Edge case: if at or very close to midnight
            Assert.True(result.Date == now.Date || result.Date == now.Date.AddDays(1));
        }
    }

    [Fact]
    public void CalculateNextRunTime_LateEveningBackupTime_HandlesCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var options = CreateOptions(new List<string> { "23:59" });
        var service = CreateService(options);

        // Act
        var result = InvokeCalculateNextRunTime(service);

        // Assert
        Assert.Equal(23, result.Hour);
        Assert.Equal(59, result.Minute);
        Assert.True(result > now, "Next run time should be in the future");
    }

    [Fact]
    public void CalculateNextRunTime_DefaultConfiguration_UsesCorrectTimes()
    {
        // Arrange - Use default configuration (08:00 and 20:00)
        var options = CreateOptions(new List<string> { "08:00", "20:00" });
        var service = CreateService(options);

        // Act
        var result = InvokeCalculateNextRunTime(service);

        // Assert
        var now = DateTime.UtcNow;
        Assert.True(result > now, "Next run time should be in the future");
        
        // Should be either 08:00 or 20:00
        Assert.True(
            (result.Hour == 8 && result.Minute == 0) || 
            (result.Hour == 20 && result.Minute == 0),
            $"Expected hour to be 8 or 20, but was {result.Hour}");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a DatabaseBackupOptions instance with the specified backup times.
    /// </summary>
    private static DatabaseBackupOptions CreateOptions(List<string> backupTimes)
    {
        return new DatabaseBackupOptions
        {
            Enabled = true,
            BackupTimes = backupTimes,
            SourceDatabasePath = "/test/source.db",
            BackupDatabasePath = "/test/backup.db"
        };
    }

    /// <summary>
    /// Creates a DatabaseBackupBackgroundService instance with the specified options.
    /// </summary>
    private static DatabaseBackupBackgroundService CreateService(DatabaseBackupOptions options)
    {
        var mockLogger = new Mock<ILogger<DatabaseBackupBackgroundService>>();
        var mockOptions = new Mock<IOptions<DatabaseBackupOptions>>();
        mockOptions.Setup(o => o.Value).Returns(options);

        return new DatabaseBackupBackgroundService(mockLogger.Object, mockOptions.Object);
    }

    /// <summary>
    /// Invokes the internal CalculateNextRunTime method directly.
    /// The method is marked internal and RTUB.Application.Tests has InternalsVisibleTo access.
    /// </summary>
    private static DateTime InvokeCalculateNextRunTime(DatabaseBackupBackgroundService service)
    {
        return service.CalculateNextRunTime();
    }

    #endregion
}
