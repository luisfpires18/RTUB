using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MeetingRequestService
/// </summary>
public class MeetingRequestServiceTests
{
    private ApplicationDbContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var httpContextAccessor = new HttpContextAccessor();
        var auditContext = new AuditContext();
        
        return new ApplicationDbContext(options, httpContextAccessor, auditContext);
    }

    private async Task<ApplicationUser> AddUserToContext(ApplicationDbContext context, string userId, string username = "testuser")
    {
        var user = new ApplicationUser 
        { 
            Id = userId, 
            UserName = username, 
            Email = $"{username}@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = username,
            PhoneNumber = "123456789"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task CreateAsync_ShouldAddMeetingRequestToDatabase()
    {
        // Arrange
        var context = GetInMemoryContext();
        context.DisableAuditing(); // Disable auditing for tests
        var service = new MeetingRequestService(context);
        var request = new MeetingRequest
        {
            Title = "Test CV Meeting",
            ProposedDateTime = DateTime.Now.AddDays(7),
            Location = "Discord",
            Description = "Test meeting description",
            AuthorUserId = "user123",
            Status = RequestStatus.Pending
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test CV Meeting", result.Title);
        Assert.Equal(1, await context.MeetingRequests.CountAsync());
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllMeetingRequests()
    {
        // Arrange
        var context = GetInMemoryContext();
        context.DisableAuditing(); // Disable auditing for tests
        var service = new MeetingRequestService(context);
        
        // Add users first
        await AddUserToContext(context, "user1", "user1");
        await AddUserToContext(context, "user2", "user2");
        
        var request1 = new MeetingRequest
        {
            Title = "Meeting 1",
            ProposedDateTime = DateTime.Now,
            Description = "Description 1",
            AuthorUserId = "user1"
        };
        
        var request2 = new MeetingRequest
        {
            Title = "Meeting 2",
            ProposedDateTime = DateTime.Now,
            Description = "Description 2",
            AuthorUserId = "user2"
        };

        context.MeetingRequests.Add(request1);
        context.MeetingRequests.Add(request2);
        await context.SaveChangesAsync();

        // Act
        var results = await service.GetAllAsync();

        // Assert
        Assert.Equal(2, results.Count());
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCorrectMeetingRequest()
    {
        // Arrange
        var context = GetInMemoryContext();
        context.DisableAuditing();
        var service = new MeetingRequestService(context);
        
        // Add user first
        await AddUserToContext(context, "user123", "user123");
        
        var request = new MeetingRequest
        {
            Title = "Test Meeting",
            ProposedDateTime = DateTime.Now,
            Description = "Test description",
            AuthorUserId = "user123"
        };

        context.MeetingRequests.Add(request);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(request.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Meeting", result.Title);
        Assert.Equal("user123", result.AuthorUserId);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldUpdateRequestStatus()
    {
        // Arrange
        var context = GetInMemoryContext();
        context.DisableAuditing();
        var service = new MeetingRequestService(context);
        
        // Add user first
        await AddUserToContext(context, "user123", "user123");
        
        var request = new MeetingRequest
        {
            Title = "Test Meeting",
            ProposedDateTime = DateTime.Now,
            Description = "Test description",
            AuthorUserId = "user123",
            Status = RequestStatus.Pending
        };

        context.MeetingRequests.Add(request);
        await context.SaveChangesAsync();

        // Act
        await service.UpdateStatusAsync(request.Id, RequestStatus.Confirmed);
        
        // Create new service instance with same context to simulate a fresh query
        var verifyService = new MeetingRequestService(context);
        var updated = await verifyService.GetByIdAsync(request.Id);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(RequestStatus.Confirmed, updated.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithInvalidId_ShouldThrowException()
    {
        // Arrange
        var context = GetInMemoryContext();
        context.DisableAuditing();
        var service = new MeetingRequestService(context);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.UpdateStatusAsync(999, RequestStatus.Confirmed)
        );
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveMeetingRequest()
    {
        // Arrange
        var context = GetInMemoryContext();
        context.DisableAuditing();
        var service = new MeetingRequestService(context);
        
        var request = new MeetingRequest
        {
            Title = "Test Meeting",
            ProposedDateTime = DateTime.Now,
            Description = "Test description",
            AuthorUserId = "user123"
        };

        context.MeetingRequests.Add(request);
        await context.SaveChangesAsync();
        var createdId = request.Id;

        // Act
        await service.DeleteAsync(createdId);

        // Assert
        var deleted = await service.GetByIdAsync(createdId);
        Assert.Null(deleted);
        Assert.Equal(0, await context.MeetingRequests.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidId_ShouldThrowException()
    {
        // Arrange
        var context = GetInMemoryContext();
        context.DisableAuditing();
        var service = new MeetingRequestService(context);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.DeleteAsync(999)
        );
    }

    [Fact]
    public async Task GetAllAsync_ShouldOrderByCreatedAtDescending()
    {
        // Arrange
        var context = GetInMemoryContext();
        context.DisableAuditing();
        var service = new MeetingRequestService(context);
        
        // Add users first
        await AddUserToContext(context, "user1", "user1");
        await AddUserToContext(context, "user2", "user2");
        
        var request1 = new MeetingRequest
        {
            Title = "First",
            ProposedDateTime = DateTime.Now,
            Description = "Description 1",
            AuthorUserId = "user1",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        
        var request2 = new MeetingRequest
        {
            Title = "Second",
            ProposedDateTime = DateTime.Now,
            Description = "Description 2",
            AuthorUserId = "user2",
            CreatedAt = DateTime.UtcNow
        };
        
        context.MeetingRequests.Add(request1);
        context.MeetingRequests.Add(request2);
        await context.SaveChangesAsync();

        // Act
        var results = (await service.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Second", results[0].Title); // Most recent first
        Assert.Equal("First", results[1].Title);
    }

    [Fact]
    public async Task GetAllAsync_WithStatusFilter_ShouldReturnFilteredRequests()
    {
        // Arrange
        var context = GetInMemoryContext();
        context.DisableAuditing();
        var service = new MeetingRequestService(context);
        
        // Add users first
        await AddUserToContext(context, "user1", "user1");
        await AddUserToContext(context, "user2", "user2");
        
        var pending = new MeetingRequest
        {
            Title = "Pending",
            ProposedDateTime = DateTime.Now,
            Description = "Pending request",
            AuthorUserId = "user1",
            Status = RequestStatus.Pending
        };
        
        var confirmed = new MeetingRequest
        {
            Title = "Confirmed",
            ProposedDateTime = DateTime.Now,
            Description = "Confirmed request",
            AuthorUserId = "user2",
            Status = RequestStatus.Confirmed
        };
        
        context.MeetingRequests.AddRange(pending, confirmed);
        await context.SaveChangesAsync();

        // Act
        var results = (await service.GetAllAsync(RequestStatus.Pending)).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal("Pending", results[0].Title);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnCorrectPage()
    {
        // Arrange
        var context = GetInMemoryContext();
        context.DisableAuditing();
        var service = new MeetingRequestService(context);
        
        // Add user first
        await AddUserToContext(context, "user1", "user1");
        
        // Add 5 requests
        for (int i = 1; i <= 5; i++)
        {
            context.MeetingRequests.Add(new MeetingRequest
            {
                Title = $"Meeting {i}",
                ProposedDateTime = DateTime.Now,
                Description = $"Description {i}",
                AuthorUserId = "user1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await context.SaveChangesAsync();

        // Act
        var page1 = (await service.GetPagedAsync(1, 2)).ToList();
        var page2 = (await service.GetPagedAsync(2, 2)).ToList();

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
        var context = GetInMemoryContext();
        context.DisableAuditing();
        var service = new MeetingRequestService(context);
        
        // Add user first
        await AddUserToContext(context, "user1", "user1");
        
        // Add 3 pending and 2 confirmed requests
        for (int i = 1; i <= 3; i++)
        {
            context.MeetingRequests.Add(new MeetingRequest
            {
                Title = $"Pending {i}",
                ProposedDateTime = DateTime.Now,
                Description = $"Description {i}",
                AuthorUserId = "user1",
                Status = RequestStatus.Pending
            });
        }
        
        for (int i = 1; i <= 2; i++)
        {
            context.MeetingRequests.Add(new MeetingRequest
            {
                Title = $"Confirmed {i}",
                ProposedDateTime = DateTime.Now,
                Description = $"Description {i}",
                AuthorUserId = "user1",
                Status = RequestStatus.Confirmed
            });
        }
        await context.SaveChangesAsync();

        // Act
        var results = (await service.GetPagedAsync(1, 10, RequestStatus.Pending)).ToList();

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Equal(RequestStatus.Pending, r.Status));
    }

    [Fact]
    public async Task GetTotalCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var context = GetInMemoryContext();
        context.DisableAuditing();
        var service = new MeetingRequestService(context);
        
        context.MeetingRequests.AddRange(
            new MeetingRequest { Title = "1", ProposedDateTime = DateTime.Now, Description = "D", AuthorUserId = "u1" },
            new MeetingRequest { Title = "2", ProposedDateTime = DateTime.Now, Description = "D", AuthorUserId = "u1" },
            new MeetingRequest { Title = "3", ProposedDateTime = DateTime.Now, Description = "D", AuthorUserId = "u1" }
        );
        await context.SaveChangesAsync();

        // Act
        var count = await service.GetTotalCountAsync();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetTotalCountAsync_WithStatusFilter_ShouldReturnFilteredCount()
    {
        // Arrange
        var context = GetInMemoryContext();
        context.DisableAuditing();
        var service = new MeetingRequestService(context);
        
        context.MeetingRequests.AddRange(
            new MeetingRequest { Title = "1", ProposedDateTime = DateTime.Now, Description = "D", AuthorUserId = "u1", Status = RequestStatus.Pending },
            new MeetingRequest { Title = "2", ProposedDateTime = DateTime.Now, Description = "D", AuthorUserId = "u1", Status = RequestStatus.Pending },
            new MeetingRequest { Title = "3", ProposedDateTime = DateTime.Now, Description = "D", AuthorUserId = "u1", Status = RequestStatus.Confirmed }
        );
        await context.SaveChangesAsync();

        // Act
        var count = await service.GetTotalCountAsync(RequestStatus.Pending);

        // Assert
        Assert.Equal(2, count);
    }
}
