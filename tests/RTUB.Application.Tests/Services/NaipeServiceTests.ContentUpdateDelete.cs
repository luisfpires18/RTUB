// NaipeService: content update delete.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class NaipeServiceTests
{
    [Fact]
    public async Task UpdateContentAsync_AsOwner_UpdatesContent()
    {
        // Arrange
        var content = NaipeContent.Create(
            InstrumentType.Guitarra, "Original Title", "https://example.com/video.mp4",
            "video/mp4", true, 1.0m, _testUser1.Id);
        _context.NaipeContents.Add(content);
        await _context.SaveChangesAsync();

        var newTitle = "Updated Title";
        var newDescription = "Updated Description";
        var newSortOrder = 2.0m;

        // Act
        await _naipeService.UpdateContentAsync(content.Id, newTitle, newDescription, newSortOrder, _testUser1.Id, false);

        // Assert
        _context.ChangeTracker.Clear();
        var updatedContent = await _context.NaipeContents.FindAsync(content.Id);
        updatedContent.Should().NotBeNull();
        updatedContent!.Title.Should().Be(newTitle);
        updatedContent.Description.Should().Be(newDescription);
        updatedContent.SortOrder.Should().Be(newSortOrder);
    }

    [Fact]
    public async Task UpdateContentAsync_AsAdmin_UpdatesContent()
    {
        // Arrange
        var content = NaipeContent.Create(
            InstrumentType.Baixo, "Original Title", "https://example.com/video.mp4",
            "video/mp4", true, 1.0m, _testUser1.Id);
        _context.NaipeContents.Add(content);
        await _context.SaveChangesAsync();

        var newTitle = "Admin Updated Title";

        // Act
        await _naipeService.UpdateContentAsync(content.Id, newTitle, null, 1.0m, _adminUser.Id, true);

        // Assert
        _context.ChangeTracker.Clear();
        var updatedContent = await _context.NaipeContents.FindAsync(content.Id);
        updatedContent.Should().NotBeNull();
        updatedContent!.Title.Should().Be(newTitle);
    }

    [Fact]
    public async Task UpdateContentAsync_AsOtherUser_ThrowsUnauthorizedException()
    {
        // Arrange
        var content = NaipeContent.Create(
            InstrumentType.Bandolim, "Original Title", "https://example.com/video.mp4",
            "video/mp4", true, 1.0m, _testUser1.Id);
        _context.NaipeContents.Add(content);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await _naipeService.UpdateContentAsync(content.Id, "Hacked Title", null, 1.0m, _testUser2.Id, false));
    }

    [Fact]
    public async Task DeleteContentAsync_AsOwner_DeletesContent()
    {
        // Arrange
        var content = NaipeContent.Create(
            InstrumentType.Guitarra, "To Delete", "https://example.com/video.mp4",
            "video/mp4", true, 1.0m, _testUser1.Id);
        _context.NaipeContents.Add(content);
        await _context.SaveChangesAsync();

        var mediaUrl = content.Url;
        _mockMediaStorageService.Setup(m => m.DeleteMediaAsync(mediaUrl))
            .Returns(Task.CompletedTask);

        // Act
        await _naipeService.DeleteContentAsync(content.Id, _testUser1.Id, false);

        // Assert
        _context.ChangeTracker.Clear();
        var deletedContent = await _context.NaipeContents.FindAsync(content.Id);
        deletedContent.Should().BeNull();
        _mockMediaStorageService.Verify(m => m.DeleteMediaAsync(mediaUrl), Times.Once);
    }

    [Fact]
    public async Task DeleteContentAsync_AsAdmin_DeletesContent()
    {
        // Arrange
        var content = NaipeContent.Create(
            InstrumentType.Baixo, "To Delete", "https://example.com/video.mp4",
            "video/mp4", true, 1.0m, _testUser1.Id);
        _context.NaipeContents.Add(content);
        await _context.SaveChangesAsync();

        var mediaUrl = content.Url;
        _mockMediaStorageService.Setup(m => m.DeleteMediaAsync(mediaUrl))
            .Returns(Task.CompletedTask);

        // Act
        await _naipeService.DeleteContentAsync(content.Id, _adminUser.Id, true);

        // Assert
        _context.ChangeTracker.Clear();
        var deletedContent = await _context.NaipeContents.FindAsync(content.Id);
        deletedContent.Should().BeNull();
        _mockMediaStorageService.Verify(m => m.DeleteMediaAsync(mediaUrl), Times.Once);
    }

    [Fact]
    public async Task DeleteContentAsync_AsOtherUser_ThrowsUnauthorizedException()
    {
        // Arrange
        var content = NaipeContent.Create(
            InstrumentType.Bandolim, "To Delete", "https://example.com/video.mp4",
            "video/mp4", true, 1.0m, _testUser1.Id);
        _context.NaipeContents.Add(content);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await _naipeService.DeleteContentAsync(content.Id, _testUser2.Id, false));

        // Verify content was NOT deleted
        var contentStillExists = await _context.NaipeContents.FindAsync(content.Id);
        contentStillExists.Should().NotBeNull();
        _mockMediaStorageService.Verify(m => m.DeleteMediaAsync(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// #226 F16: create, update and delete leave ONE audit entry each - the automatic one, with the fields - instead of
    /// the automatic entry plus a manual sentence for the same action. "Video Played" stays (plays are not audited
    /// automatically: NaipePlayCount is excluded).
    /// </summary>
    [Fact]
    public async Task Crud_LeavesOneAuditEntryPerAction_AndPlaysStillLogVideoPlayed()
    {
        _mockMediaStorageService.Setup(m => m.UploadVideoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://example.com/videos/f16.mp4");

        var created = await _naipeService.CreateContentAsync(
            InstrumentType.Guitarra, "F16 Video", null, new MemoryStream(new byte[] { 1 }), "f16.mp4", "video/mp4", true, 1m, _testUser1.Id);
        (await NaipeAuditAsync(created.Id)).Should().Equal(new[] { "Created" }, "no second, manual \"Video Created\" entry");
        (await LastNaipeChangesAsync(created.Id)).Should().Contain("F16 Video");

        await _naipeService.UpdateContentAsync(created.Id, "F16 Renamed", null, 2m, _testUser1.Id, false);
        (await NaipeAuditAsync(created.Id)).Should().Equal("Created", "Modified");
        (await LastNaipeChangesAsync(created.Id)).Should().Contain("F16 Video").And.Contain("F16 Renamed");

        await _naipeService.IncrementPlayCountAsync(created.Id, _testUser2.Id);
        (await NaipeAuditAsync(created.Id)).Should().Equal("Created", "Modified", "Video Played");

        await _naipeService.DeleteContentAsync(created.Id, _testUser1.Id, false);
        (await NaipeAuditAsync(created.Id)).Should().Equal("Created", "Modified", "Video Played", "Deleted");
        (await _context.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityType == "NaipeContent" && a.EntityId == created.Id && a.Action == "Deleted"))
            .IsCriticalAction.Should().BeTrue("deletes stay critical (every Deleted entry is)");
    }
}
