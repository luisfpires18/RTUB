// MeetingRequestService: commands.
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Services;
using RTUB.Application.Tests.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class MeetingRequestServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldAddMeetingRequestToDatabase()
    {
        // Arrange
        var request = new MeetingRequest
        {
            Title = "Test CV Meeting",
            ProposedDateTime = DateTime.Now.AddDays(7),
            Location = "Discord",
            Description = "Test meeting description",
            AuthorUserId = "user123",
            Status = RequestStatus.Pending
        };

        var createdRequest = new MeetingRequest
        {
            Id = 1,
            Title = "Test CV Meeting",
            ProposedDateTime = request.ProposedDateTime,
            Location = "Discord",
            Description = "Test meeting description",
            AuthorUserId = "user123",
            Status = RequestStatus.Pending
        };

        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<MeetingRequest>()))
            .ReturnsAsync(createdRequest);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test CV Meeting", result.Title);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldUpdateRequestStatus()
    {
        // Arrange
        var request = new MeetingRequest
        {
            Id = 1,
            Title = "Test Meeting",
            ProposedDateTime = DateTime.Now,
            Description = "Test description",
            AuthorUserId = "user123",
            Status = RequestStatus.Pending
        };

        var updatedRequest = new MeetingRequest
        {
            Id = 1,
            Title = "Test Meeting",
            ProposedDateTime = request.ProposedDateTime,
            Description = "Test description",
            AuthorUserId = "user123",
            Status = RequestStatus.Confirmed
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(request);
        _repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<MeetingRequest>()))
            .Returns(Task.CompletedTask);
        _repositoryMock.Setup(r => r.GetByIdWithAuthorAsync(1))
            .ReturnsAsync(updatedRequest);

        // Act
        await _service.UpdateStatusAsync(request.Id, RequestStatus.Confirmed);
        var updated = await _service.GetByIdAsync(request.Id);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(RequestStatus.Confirmed, updated.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithInvalidId_ShouldThrowException()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((MeetingRequest?)null);

        // Act & Assert
        await Assert.ThrowsAsync<RTUB.Core.Exceptions.EntityNotFoundException>(
            async () => await _service.UpdateStatusAsync(999, RequestStatus.Confirmed)
        );
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveMeetingRequest()
    {
        // Arrange
        var request = new MeetingRequest
        {
            Id = 1,
            Title = "Test Meeting",
            ProposedDateTime = DateTime.Now,
            Description = "Test description",
            AuthorUserId = "user123"
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(request);
        _repositoryMock.Setup(r => r.DeleteAsync(1))
            .Returns(Task.CompletedTask);
        _repositoryMock.Setup(r => r.GetByIdWithAuthorAsync(1))
            .ReturnsAsync((MeetingRequest?)null);

        // Act
        await _service.DeleteAsync(request.Id);

        // Assert
        var deleted = await _service.GetByIdAsync(request.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidId_ShouldThrowException()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((MeetingRequest?)null);

        // Act & Assert
        await Assert.ThrowsAsync<RTUB.Core.Exceptions.EntityNotFoundException>(
            async () => await _service.DeleteAsync(999)
        );
    }
}
