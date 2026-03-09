using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for RequestService
/// Tests request status workflow and notifications
/// </summary>
public class RequestServiceTests
{
    private readonly Mock<IRequestRepository> _mockRequestRepository;
    private readonly RequestService _requestService;

    public RequestServiceTests()
    {
        _mockRequestRepository = new Mock<IRequestRepository>();

        // Create mocks for new dependencies
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        _requestService = new RequestService(
            _mockRequestRepository.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            mockHttpContextAccessor.Object);
    }

    [Fact]
    public async Task CreateRequestAsync_WithValidData_CreatesRequest()
    {
        // Arrange
        var name = "John Doe";
        var email = "john@example.com";
        var phone = "123456789";
        var eventType = "Wedding";
        var preferredDate = DateTime.Now.AddDays(30);
        var location = "Test Venue";
        var message = "Looking for performance";
        var expectedRequest = Request.Create(name, email, phone, eventType, preferredDate, location, message);

        _mockRequestRepository.Setup(r => r.AddAsync(It.IsAny<Request>()))
            .ReturnsAsync(expectedRequest);

        // Act
        var result = await _requestService.CreateRequestAsync(name, email, phone, eventType, preferredDate, location, message);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Email.Should().Be(email);
        result.Status.Should().Be(RequestStatus.Pending);
    }

    [Fact]
    public async Task GetRequestByIdAsync_ExistingRequest_ReturnsRequest()
    {
        // Arrange
        var request = Request.Create("John", "john@test.com", "123456", "Wedding", DateTime.Now.AddDays(30), "Venue", "Message");
        _mockRequestRepository.Setup(r => r.GetByIdAsync(request.Id))
            .ReturnsAsync(request);

        // Act
        var result = await _requestService.GetRequestByIdAsync(request.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(request.Id);
    }

    [Fact]
    public async Task GetRequestByIdAsync_NonExistingRequest_ReturnsNull()
    {
        // Arrange
        _mockRequestRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Request?)null);

        // Act
        var result = await _requestService.GetRequestByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllRequestsAsync_WithMultipleRequests_ReturnsAll()
    {
        // Arrange
        var requests = new List<Request>
        {
            Request.Create("John1", "john1@test.com", "111", "Wedding", DateTime.Now.AddDays(30), "Venue1", "Msg1"),
            Request.Create("John2", "john2@test.com", "222", "Festival", DateTime.Now.AddDays(31), "Venue2", "Msg2"),
            Request.Create("John3", "john3@test.com", "333", "Concert", DateTime.Now.AddDays(32), "Venue3", "Msg3")
        };
        _mockRequestRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(requests);

        // Act
        var result = await _requestService.GetAllRequestsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPendingRequestsAsync_OnlyReturnsPending()
    {
        // Arrange
        var pendingRequests = new List<Request>
        {
            Request.Create("John1", "john1@test.com", "111", "Wedding", DateTime.Now.AddDays(30), "Venue1", "Msg1"),
            Request.Create("John3", "john3@test.com", "333", "Concert", DateTime.Now.AddDays(32), "Venue3", "Msg3")
        };
        _mockRequestRepository.Setup(r => r.GetByStatusAsync(RequestStatus.Pending))
            .ReturnsAsync(pendingRequests);

        // Act
        var result = await _requestService.GetPendingRequestsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.Status == RequestStatus.Pending);
    }

    [Fact]
    public async Task GetPendingRequestsAsync_OrdersByPreferredDate()
    {
        // Arrange
        var requests = new List<Request>
        {
            Request.Create("John1", "john1@test.com", "111", "Wedding", DateTime.Now.AddDays(35), "Venue1", "Msg1"),
            Request.Create("John2", "john2@test.com", "222", "Festival", DateTime.Now.AddDays(30), "Venue2", "Msg2"),
            Request.Create("John3", "john3@test.com", "333", "Concert", DateTime.Now.AddDays(32), "Venue3", "Msg3")
        }.OrderBy(r => r.PreferredDate).ToList();

        _mockRequestRepository.Setup(r => r.GetByStatusAsync(RequestStatus.Pending))
            .ReturnsAsync(requests);

        // Act
        var result = (await _requestService.GetPendingRequestsAsync()).ToList();

        // Assert
        result[0].Name.Should().Be("John2"); // Earliest date
        result[1].Name.Should().Be("John3");
        result[2].Name.Should().Be("John1"); // Latest date
    }

    [Fact]
    public async Task SetRequestDateRangeAsync_SetsEndDate()
    {
        // Arrange
        var request = Request.Create("John", "john@test.com", "123456", "Wedding", DateTime.Now.AddDays(30), "Venue", "Message");
        var endDate = DateTime.Now.AddDays(32);

        _mockRequestRepository.Setup(r => r.GetByIdAsync(request.Id))
            .ReturnsAsync(request);

        // Act
        await _requestService.SetRequestDateRangeAsync(request.Id, endDate);

        // Assert
        request.PreferredEndDate.Should().Be(endDate);
        request.IsDateRange.Should().BeTrue();
        _mockRequestRepository.Verify(r => r.UpdateAsync(request), Times.Once);
    }

    [Fact]
    public async Task SetRequestDateRangeAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        _mockRequestRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Request?)null);

        // Act & Assert
        var act = async () => await _requestService.SetRequestDateRangeAsync(999, DateTime.Now.AddDays(1));
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateRequestStatusAsync_UpdatesStatus()
    {
        // Arrange
        var request = Request.Create("John", "john@test.com", "123456", "Wedding", DateTime.Now.AddDays(30), "Venue", "Message");
        _mockRequestRepository.Setup(r => r.GetByIdAsync(request.Id))
            .ReturnsAsync(request);

        // Act
        await _requestService.UpdateRequestStatusAsync(request.Id, RequestStatus.Analysing);

        // Assert
        request.Status.Should().Be(RequestStatus.Analysing);
        _mockRequestRepository.Verify(r => r.UpdateAsync(request), Times.Once);
    }

    [Fact]
    public async Task UpdateRequestStatusAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        _mockRequestRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Request?)null);

        // Act & Assert
        var act = async () => await _requestService.UpdateRequestStatusAsync(999, RequestStatus.Confirmed);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteRequestAsync_RemovesRequest()
    {
        // Arrange
        var request = Request.Create("John", "john@test.com", "123456", "Wedding", DateTime.Now.AddDays(30), "Venue", "Message");
        _mockRequestRepository.Setup(r => r.GetByIdAsync(request.Id))
            .ReturnsAsync(request);

        // Act
        await _requestService.DeleteRequestAsync(request.Id);

        // Assert
        _mockRequestRepository.Verify(r => r.DeleteAsync(It.IsAny<Request>()), Times.Once);
    }

    [Fact]
    public async Task DeleteRequestAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        _mockRequestRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Request?)null);

        // Act & Assert
        var act = async () => await _requestService.DeleteRequestAsync(999);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task SetRequestDateRangeAsync_WithValidRequest_UpdatesRequest()
    {
        // Arrange
        var request = Request.Create("John", "john@test.com", "123456", "Wedding", DateTime.Now.AddDays(30), "Venue", "Message");
        var endDate = DateTime.Now.AddDays(35);
        _mockRequestRepository.Setup(r => r.GetByIdAsync(request.Id))
            .ReturnsAsync(request);

        // Act
        await _requestService.SetRequestDateRangeAsync(request.Id, endDate);

        // Assert
        request.PreferredEndDate.Should().Be(endDate);
        request.IsDateRange.Should().BeTrue();
        _mockRequestRepository.Verify(r => r.UpdateAsync(request), Times.Once);
    }
}
