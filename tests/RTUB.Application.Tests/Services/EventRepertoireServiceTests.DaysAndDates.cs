using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

// EventRepertoireServiceTests: Days And Dates tests.
public partial class EventRepertoireServiceTests
{
    [Fact]
    public async Task RemoveRepertoireDayAsync_RemovesOnlyThatDay()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Multi Day Event", _testEventDate, "Test Location", EventType.Festival);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Day 1 Song", album.Id);
        var song2 = await _songService.CreateSongAsync("Day 2 Song", album.Id);

        var day1 = _testEventDate;
        var day2 = _testEventDate.AddDays(1);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 1, day2);

        // Act - Remove day 1
        await _repertoireService.RemoveRepertoireDayAsync(event1.Id, day1);

        // Assert
        var day1Result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day1)).ToList();
        var day2Result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day2)).ToList();

        day1Result.Should().BeEmpty();
        day2Result.Should().ContainSingle();
        day2Result[0].Song!.Title.Should().Be("Day 2 Song");
    }

    [Fact]
    public async Task GetRepertoireDatesAsync_ReturnsDistinctDates()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Multi Day Event", _testEventDate, "Test Location", EventType.Festival);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Song 3", album.Id);

        var day1 = _testEventDate;
        var day2 = _testEventDate.AddDays(1);
        var day3 = _testEventDate.AddDays(2);

        // Add multiple songs to same days
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 1, day2);

        // Act
        var dates = (await _repertoireService.GetRepertoireDatesAsync(event1.Id)).ToList();

        // Assert
        dates.Should().HaveCount(2);
        dates.Should().Contain(day1.Date);
        dates.Should().Contain(day2.Date);
        dates.Should().NotContain(day3.Date);
    }

    [Fact]
    public async Task GetRepertoireDatesAsync_EmptyRepertoire_ReturnsEmpty()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Empty Event", _testEventDate, "Test Location", EventType.Atuacao);

        // Act
        var dates = (await _repertoireService.GetRepertoireDatesAsync(event1.Id)).ToList();

        // Assert
        dates.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveRepertoireDayAsync_WithMultipleSongs_RemovesAllSongsForThatDay()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Multi Day Event", _testEventDate, "Test Location", EventType.Festival);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Song 3", album.Id);

        var day1 = _testEventDate;

        // Add multiple songs to the same day
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3, day1);

        // Verify songs are added
        var beforeDelete = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day1)).ToList();
        beforeDelete.Should().HaveCount(3);

        // Act - Remove the entire day
        await _repertoireService.RemoveRepertoireDayAsync(event1.Id, day1);

        // Assert - All songs for that day should be removed
        var afterDelete = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day1)).ToList();
        afterDelete.Should().BeEmpty();

        // Verify dates list is also empty
        var dates = (await _repertoireService.GetRepertoireDatesAsync(event1.Id)).ToList();
        dates.Should().BeEmpty();
    }

    [Fact]
    public async Task MultiDayEvent_PerDayDisplayOrder_StartsAt1ForEachDay()
    {
        // Arrange - Create multi-day event with songs on different days
        var event1 = await _eventService.CreateEventAsync("Multi Day Festival", _testEventDate, "Test Location", EventType.Festival);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Day 1 Song A", album.Id);
        var song2 = await _songService.CreateSongAsync("Day 1 Song B", album.Id);
        var song3 = await _songService.CreateSongAsync("Day 1 Song C", album.Id);
        var song4 = await _songService.CreateSongAsync("Day 2 Song A", album.Id);
        var song5 = await _songService.CreateSongAsync("Day 2 Song B", album.Id);

        var day1 = _testEventDate;
        var day2 = _testEventDate.AddDays(1);

        // Add songs with specific display orders - Day 1 gets 1-3, Day 2 should also get 1-2
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song4.Id, 1, day2);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song5.Id, 2, day2);

        // Act - Get repertoire for each day
        var day1Result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day1)).OrderBy(r => r.DisplayOrder).ToList();
        var day2Result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day2)).OrderBy(r => r.DisplayOrder).ToList();

        // Assert - Each day should have its own 1-based ordering
        // Day 1: 3 songs with DisplayOrder 1, 2, 3 (UI would show them as 1, 2, 3)
        day1Result.Should().HaveCount(3);
        day1Result[0].DisplayOrder.Should().Be(1);
        day1Result[0].Song!.Title.Should().Be("Day 1 Song A");
        day1Result[1].DisplayOrder.Should().Be(2);
        day1Result[1].Song!.Title.Should().Be("Day 1 Song B");
        day1Result[2].DisplayOrder.Should().Be(3);
        day1Result[2].Song!.Title.Should().Be("Day 1 Song C");

        // Day 2: 2 songs with DisplayOrder 1, 2 (UI would show them as 1, 2)
        day2Result.Should().HaveCount(2);
        day2Result[0].DisplayOrder.Should().Be(1);
        day2Result[0].Song!.Title.Should().Be("Day 2 Song A");
        day2Result[1].DisplayOrder.Should().Be(2);
        day2Result[1].Song!.Title.Should().Be("Day 2 Song B");

        // Verify that when ordered, UI display index calculation works correctly
        // For day 1: orderedItems[0, 1, 2] => displayIndex = i + 1 => [1, 2, 3]
        for (int i = 0; i < day1Result.Count; i++)
        {
            var displayIndex = i + 1;
            day1Result[i].DisplayOrder.Should().Be(displayIndex,
                $"Day 1 song at position {i} should have DisplayOrder {displayIndex}");
        }

        // For day 2: orderedItems[0, 1] => displayIndex = i + 1 => [1, 2]
        for (int i = 0; i < day2Result.Count; i++)
        {
            var displayIndex = i + 1;
            day2Result[i].DisplayOrder.Should().Be(displayIndex,
                $"Day 2 song at position {i} should have DisplayOrder {displayIndex}");
        }
    }
}
