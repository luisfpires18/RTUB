// MeetingRequestService: queries.
using Moq;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class MeetingRequestServiceTests
{
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
}
