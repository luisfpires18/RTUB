using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using System.Security.Claims;

namespace RTUB.Application.Tests.Data;

/// <summary>
/// Tests to verify that login operations create audit logs with proper UserName
/// Reproduces the bug where users logging in don't appear in the tracing page
/// </summary>
public class LoginAuditLogTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly AuditContext _auditContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public LoginAuditLogTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _auditContext = new AuditContext();
        
        // Set up HttpContext without an authenticated user (simulating pre-login state)
        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(x => x.User).Returns(new ClaimsPrincipal());
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContextMock.Object);

        _context = new ApplicationDbContext(options, _httpContextAccessorMock.Object, _auditContext);
        
        // Set up UserManager
        var userStore = new UserStore<ApplicationUser>(_context);
        _userManager = new UserManager<ApplicationUser>(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
    }

    [Fact]
    public async Task Login_UpdateLastLoginDate_DoesNotCreateAuditLog()
    {
        // Arrange - Create a user (simulating existing user in database)
        var user = new ApplicationUser
        {
            UserName = "johndoe",
            Email = "john@example.com",
            FirstName = "John",
            LastName = "Doe",
            Nickname = "JDoe",
            PhoneContact = "123456789",
            EmailConfirmed = true
        };
        
        var result = await _userManager.CreateAsync(user, "Password123!");
        result.Succeeded.Should().BeTrue();
        
        // Clear audit logs from user creation
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();
        
        // Act - Simulate login endpoint behavior
        // 1. Set audit context (this is what Program.cs does on line 386)
        _auditContext.SetUser(user.UserName, user.Id);
        
        // 2. Update LastLoginDate (this is what Program.cs does on line 388-389)
        user.LastLoginDate = DateTime.UtcNow;
        var updateResult = await _userManager.UpdateAsync(user);
        
        // 3. Clear audit context (this is what Program.cs does on line 404)
        _auditContext.Clear();
        
        // Assert
        updateResult.Succeeded.Should().BeTrue();
        
        // LastLoginDate is now excluded from audit logs as it's tracked separately in application logs
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().BeEmpty("LastLoginDate changes should not create audit logs");
    }
    
    [Fact]
    public async Task Login_WithoutAuditContext_DoesNotCreateAuditLog()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "janedoe",
            Email = "jane@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            Nickname = "JaneDoe",
            PhoneContact = "987654321",
            EmailConfirmed = true
        };
        
        var result = await _userManager.CreateAsync(user, "Password123!");
        result.Succeeded.Should().BeTrue();
        
        // Clear audit logs from user creation
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();
        
        // Act - Simulate login WITHOUT setting audit context
        // DON'T set audit context: _auditContext.SetUser(user.UserName, user.Id);
        
        user.LastLoginDate = DateTime.UtcNow;
        var updateResult = await _userManager.UpdateAsync(user);
        
        // Assert
        updateResult.Succeeded.Should().BeTrue();
        
        // LastLoginDate is now excluded from audit logs
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().BeEmpty("LastLoginDate changes should not create audit logs");
    }

    public void Dispose()
    {
        _context.Dispose();
        _userManager.Dispose();
    }
}
