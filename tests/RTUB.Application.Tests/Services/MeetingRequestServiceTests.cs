using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using MockQueryable.Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MeetingRequestService
/// </summary>
public class MeetingRequestServiceTests
{
    private readonly Mock<IMeetingRequestRepository> _repositoryMock;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<ILogger<MeetingRequestService>> _mockLogger;
    private readonly MeetingRequestService _service;

    public MeetingRequestServiceTests()
    {
        _repositoryMock = new Mock<IMeetingRequestRepository>();
        _mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        _mockPushNotificationService = new Mock<IPushNotificationService>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockUserManager = MockHelpers.CreateMockUserManager();
        _mockLogger = new Mock<ILogger<MeetingRequestService>>();

        _service = new MeetingRequestService(
            _repositoryMock.Object,
            _mockPushNotificationFactory.Object,
            _mockPushNotificationService.Object,
            _mockUserManager.Object,
            _mockHttpContextAccessor.Object,
            _mockLogger.Object);
    }

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
    public async Task GetAllAsync_ShouldReturnAllMeetingRequests()
    {
        // Arrange
        var requests = new List<MeetingRequest>
        {
            new MeetingRequest
            {
                Id = 1,
                Title = "Meeting 1",
                ProposedDateTime = DateTime.Now,
                Description = "Description 1",
                AuthorUserId = "user1"
            },
            new MeetingRequest
            {
                Id = 2,
                Title = "Meeting 2",
                ProposedDateTime = DateTime.Now,
                Description = "Description 2",
                AuthorUserId = "user2"
            }
        };

        _repositoryMock.Setup(r => r.GetAllWithAuthorAsync(null))
            .ReturnsAsync(requests);

        // Act
        var results = await _service.GetAllAsync();

        // Assert
        Assert.Equal(2, results.Count());
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCorrectMeetingRequest()
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

        _repositoryMock.Setup(r => r.GetByIdWithAuthorAsync(1))
            .ReturnsAsync(request);

        // Act
        var result = await _service.GetByIdAsync(request.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Meeting", result.Title);
        Assert.Equal("user123", result.AuthorUserId);
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
        await Assert.ThrowsAsync<InvalidOperationException>(
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
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.DeleteAsync(999)
        );
    }

    [Fact]
    public async Task GetAllAsync_ShouldOrderByCreatedAtDescending()
    {
        // Arrange
        var requests = new List<MeetingRequest>
        {
            new MeetingRequest
            {
                Id = 1,
                Title = "First",
                ProposedDateTime = DateTime.Now,
                Description = "Description 1",
                AuthorUserId = "user1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new MeetingRequest
            {
                Id = 2,
                Title = "Second",
                ProposedDateTime = DateTime.Now,
                Description = "Description 2",
                AuthorUserId = "user2",
                CreatedAt = DateTime.UtcNow
            }
        };

        _repositoryMock.Setup(r => r.GetAllWithAuthorAsync(null))
            .ReturnsAsync(requests);

        // Act
        var results = (await _service.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Second", results[0].Title); // Most recent first
        Assert.Equal("First", results[1].Title);
    }

    [Fact]
    public async Task GetAllAsync_WithStatusFilter_ShouldReturnFilteredRequests()
    {
        // Arrange
        var requests = new List<MeetingRequest>
        {
            new MeetingRequest
            {
                Id = 1,
                Title = "Pending",
                ProposedDateTime = DateTime.Now,
                Description = "Pending request",
                AuthorUserId = "user1",
                Status = RequestStatus.Pending
            }
        };

        _repositoryMock.Setup(r => r.GetAllWithAuthorAsync(RequestStatus.Pending))
            .ReturnsAsync(requests);

        // Act
        var results = (await _service.GetAllAsync(RequestStatus.Pending)).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal("Pending", results[0].Title);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnCorrectPage()
    {
        // Arrange
        var requests = new List<MeetingRequest>
        {
            new MeetingRequest { Id = 1, Title = "Meeting 1", ProposedDateTime = DateTime.Now, Description = "D1", AuthorUserId = "u1", CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new MeetingRequest { Id = 2, Title = "Meeting 2", ProposedDateTime = DateTime.Now, Description = "D2", AuthorUserId = "u1", CreatedAt = DateTime.UtcNow.AddMinutes(-2) }
        };

        _repositoryMock.Setup(r => r.GetPagedWithAuthorAsync(1, 2, null))
            .ReturnsAsync(requests.Take(2).ToList());

        var requests2 = new List<MeetingRequest>
        {
            new MeetingRequest { Id = 3, Title = "Meeting 3", ProposedDateTime = DateTime.Now, Description = "D3", AuthorUserId = "u1", CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
            new MeetingRequest { Id = 4, Title = "Meeting 4", ProposedDateTime = DateTime.Now, Description = "D4", AuthorUserId = "u1", CreatedAt = DateTime.UtcNow.AddMinutes(-4) }
        };

        _repositoryMock.Setup(r => r.GetPagedWithAuthorAsync(2, 2, null))
            .ReturnsAsync(requests2);

        // Act
        var page1 = (await _service.GetPagedAsync(1, 2)).ToList();
        var page2 = (await _service.GetPagedAsync(2, 2)).ToList();

        // Assert
        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.Equal("Meeting 1", page1[0].Title); // Most recent first
        Assert.Equal("Meeting 3", page2[0].Title);
    }

    [Fact]
    public async Task GetPagedAsync_WithStatusFilter_ShouldReturnFilteredPage()
    {
        // Arrange
        var requests = new List<MeetingRequest>
        {
            new MeetingRequest { Id = 1, Title = "Pending 1", ProposedDateTime = DateTime.Now, Description = "D1", AuthorUserId = "u1", Status = RequestStatus.Pending },
            new MeetingRequest { Id = 2, Title = "Pending 2", ProposedDateTime = DateTime.Now, Description = "D2", AuthorUserId = "u1", Status = RequestStatus.Pending },
            new MeetingRequest { Id = 3, Title = "Pending 3", ProposedDateTime = DateTime.Now, Description = "D3", AuthorUserId = "u1", Status = RequestStatus.Pending }
        };

        _repositoryMock.Setup(r => r.GetPagedWithAuthorAsync(1, 10, RequestStatus.Pending))
            .ReturnsAsync(requests);

        // Act
        var results = (await _service.GetPagedAsync(1, 10, RequestStatus.Pending)).ToList();

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Equal(RequestStatus.Pending, r.Status));
    }

    [Fact]
    public async Task GetTotalCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetCountAsync(null))
            .ReturnsAsync(3);

        // Act
        var count = await _service.GetTotalCountAsync();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetTotalCountAsync_WithStatusFilter_ShouldReturnFilteredCount()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetCountAsync(RequestStatus.Pending))
            .ReturnsAsync(2);

        // Act
        var count = await _service.GetTotalCountAsync(RequestStatus.Pending);

        // Assert
        Assert.Equal(2, count);
    }
}
