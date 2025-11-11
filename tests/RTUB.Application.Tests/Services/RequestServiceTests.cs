using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for RequestService
/// Tests request status workflow and notifications
/// </summary>
public class RequestServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IEmailNotificationService> _emailServiceMock;
    private readonly RequestService _requestService;

    public RequestServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext());
        _emailServiceMock = new Mock<IEmailNotificationService>();
        _requestService = new RequestService(_context, _emailServiceMock.Object);
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

        // Act
        var result = await _requestService.CreateRequestAsync(name, email, phone, eventType, preferredDate, location, message);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Email.Should().Be(email);
        result.Status.Should().Be(RequestStatus.Pending);
        
        _emailServiceMock.Verify(
            x => x.SendNewRequestNotificationAsync(result.Id, name, email, eventType),
            Times.Once);
    }

    [Fact]
    public async Task GetRequestByIdAsync_ExistingRequest_ReturnsRequest()
    {
        // Arrange
        var request = await _requestService.CreateRequestAsync(
            "John", "john@test.com", "123456", "Wedding", DateTime.Now.AddDays(30), "Venue", "Message");

        // Act
        var result = await _requestService.GetRequestByIdAsync(request.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(request.Id);
    }

    [Fact]
    public async Task GetRequestByIdAsync_NonExistingRequest_ReturnsNull()
    {
        // Act
        var result = await _requestService.GetRequestByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllRequestsAsync_WithMultipleRequests_ReturnsAll()
    {
        // Arrange
        await _requestService.CreateRequestAsync("John1", "john1@test.com", "111", "Wedding", DateTime.Now.AddDays(30), "Venue1", "Msg1");
        await _requestService.CreateRequestAsync("John2", "john2@test.com", "222", "Festival", DateTime.Now.AddDays(31), "Venue2", "Msg2");
        await _requestService.CreateRequestAsync("John3", "john3@test.com", "333", "Concert", DateTime.Now.AddDays(32), "Venue3", "Msg3");

        // Act
        var result = await _requestService.GetAllRequestsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPendingRequestsAsync_OnlyReturnsPending()
    {
        // Arrange
        var request1 = await _requestService.CreateRequestAsync("John1", "john1@test.com", "111", "Wedding", DateTime.Now.AddDays(30), "Venue1", "Msg1");
        var request2 = await _requestService.CreateRequestAsync("John2", "john2@test.com", "222", "Festival", DateTime.Now.AddDays(31), "Venue2", "Msg2");
        var request3 = await _requestService.CreateRequestAsync("John3", "john3@test.com", "333", "Concert", DateTime.Now.AddDays(32), "Venue3", "Msg3");
        
        await _requestService.UpdateRequestStatusAsync(request2.Id, RequestStatus.Confirmed);

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
        await _requestService.CreateRequestAsync("John1", "john1@test.com", "111", "Wedding", DateTime.Now.AddDays(35), "Venue1", "Msg1");
        await _requestService.CreateRequestAsync("John2", "john2@test.com", "222", "Festival", DateTime.Now.AddDays(30), "Venue2", "Msg2");
        await _requestService.CreateRequestAsync("John3", "john3@test.com", "333", "Concert", DateTime.Now.AddDays(32), "Venue3", "Msg3");

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
        var request = await _requestService.CreateRequestAsync(
            "John", "john@test.com", "123456", "Wedding", DateTime.Now.AddDays(30), "Venue", "Message");
        var endDate = DateTime.Now.AddDays(32);

        // Act
        await _requestService.SetRequestDateRangeAsync(request.Id, endDate);
        var updated = await _requestService.GetRequestByIdAsync(request.Id);

        // Assert
        updated!.PreferredEndDate.Should().Be(endDate);
        updated.IsDateRange.Should().BeTrue();
    }

    [Fact]
    public async Task SetRequestDateRangeAsync_WithInvalidId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _requestService.SetRequestDateRangeAsync(999, DateTime.Now.AddDays(1));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateRequestStatusAsync_UpdatesStatus()
    {
        // Arrange
        var request = await _requestService.CreateRequestAsync(
            "John", "john@test.com", "123456", "Wedding", DateTime.Now.AddDays(30), "Venue", "Message");

        // Act
        await _requestService.UpdateRequestStatusAsync(request.Id, RequestStatus.Analysing);
        var updated = await _requestService.GetRequestByIdAsync(request.Id);

        // Assert
        updated!.Status.Should().Be(RequestStatus.Analysing);
    }

    [Fact]
    public async Task UpdateRequestStatusAsync_SendsNotificationOnStatusChange()
    {
        // Arrange
        var request = await _requestService.CreateRequestAsync(
            "John", "john@test.com", "123456", "Wedding", DateTime.Now.AddDays(30), "Venue", "Message");

        // Act
        await _requestService.UpdateRequestStatusAsync(request.Id, RequestStatus.Confirmed);

        // Assert
        _emailServiceMock.Verify(
            x => x.SendRequestStatusChangedAsync(
                request.Id, request.Name, request.Email, RequestStatus.Pending, RequestStatus.Confirmed),
            Times.Once);
    }

    [Fact]
    public async Task UpdateRequestStatusAsync_WithInvalidId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _requestService.UpdateRequestStatusAsync(999, RequestStatus.Confirmed);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteRequestAsync_RemovesRequest()
    {
        // Arrange
        var request = await _requestService.CreateRequestAsync(
            "John", "john@test.com", "123456", "Wedding", DateTime.Now.AddDays(30), "Venue", "Message");

        // Act
        await _requestService.DeleteRequestAsync(request.Id);
        var deleted = await _requestService.GetRequestByIdAsync(request.Id);

        // Assert
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRequestAsync_WithInvalidId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _requestService.DeleteRequestAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
