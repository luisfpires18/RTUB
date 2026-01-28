using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using System.Reflection;

namespace RTUB.Web.Tests.Pages.Base;

/// <summary>
/// Base class for page component tests
/// Provides common setup for mocking services, authentication, and JavaScript interop
/// </summary>
public abstract class PageTestBase : TestContext
{
    protected readonly Mock<IJSRuntime> MockJSRuntime;
    protected readonly Mock<ILoggerFactory> MockLoggerFactory;
    protected readonly Mock<ILogger> MockLogger;

    protected PageTestBase()
    {
        // Setup JSInterop for common JavaScript calls
        JSInterop.SetupVoid("modalHelper.lockBodyScroll");
        JSInterop.SetupVoid("modalHelper.unlockBodyScroll");
        JSInterop.SetupVoid("messageScroller.scrollToBottom");

        // Setup common mocks
        MockJSRuntime = new Mock<IJSRuntime>();
        MockLoggerFactory = new Mock<ILoggerFactory>();
        MockLogger = new Mock<ILogger>();

        MockLoggerFactory
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(MockLogger.Object);

        Services.AddSingleton(MockLoggerFactory.Object);
        Services.AddSingleton(MockJSRuntime.Object);

        // Register default mocks for all application interfaces to avoid "no registered service" failures
        // when pages inject newly extracted services.
        RegisterDefaultApplicationInterfaceMocks();
    }

    private void RegisterDefaultApplicationInterfaceMocks()
    {
        var interfacesAssembly = typeof(IMemberStatusService).Assembly;
        var interfaceTypes = interfacesAssembly
            .GetTypes()
            .Where(t =>
                t.IsInterface &&
                t.Namespace == typeof(IMemberStatusService).Namespace &&
                !t.IsGenericTypeDefinition)
            .ToList();

        foreach (var interfaceType in interfaceTypes)
        {
            if (Services.Any(sd => sd.ServiceType == interfaceType))
            {
                continue;
            }

            var mockType = typeof(Mock<>).MakeGenericType(interfaceType);
            var mockInstance = Activator.CreateInstance(mockType);
            if (mockInstance is null)
            {
                continue;
            }

            if (mockInstance is Mock baseMock)
            {
                baseMock.DefaultValue = DefaultValue.Empty;
                Services.AddSingleton(interfaceType, baseMock.Object);
            }
        }
    }

    /// <summary>
    /// Sets up authentication for the test context.
    /// Uses fluent AddTestAuthorization().SetAuthorized().SetRoles() so IsInRole works in components.
    /// </summary>
    /// <param name="userId">User ID (default: "test-user")</param>
    /// <param name="userName">User name (default: "Test User")</param>
    /// <param name="roles">User roles (default: empty)</param>
    protected void SetupAuthentication(string userId = "test-user", string userName = "Test User", params string[] roles)
    {
        var authState = this.AddTestAuthorization().SetAuthorized(userId);
        foreach (var role in roles)
        {
            authState.SetRoles(role);
        }
    }

    /// <summary>
    /// Sets up authentication as Admin. Use for tests that assert admin-only UI (e.g. create button).
    /// </summary>
    /// <param name="userId">User ID (default: "admin-user")</param>
    protected void SetupAuthenticationAsAdmin(string userId = "admin-user")
    {
        this.AddTestAuthorization().SetAuthorized(userId).SetRoles("Admin");
    }

    /// <summary>
    /// Sets up an unauthenticated user
    /// </summary>
    protected void SetupUnauthenticated()
    {
        this.AddTestAuthorization();
    }

    /// <summary>
    /// Sets up a mock UserManager
    /// </summary>
    protected Mock<UserManager<ApplicationUser>> SetupUserManager()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        Services.AddSingleton(userManager.Object);
        return userManager;
    }

    /// <summary>
    /// Sets up a mock service and adds it to the service collection
    /// </summary>
    protected Mock<T> SetupService<T>() where T : class
    {
        var mock = new Mock<T>();
        Services.AddSingleton(mock.Object);
        return mock;
    }

    /// <summary>
    /// Sets up a mock IWebHostEnvironment
    /// </summary>
    protected Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment> SetupWebHostEnvironment()
    {
        var mock = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        mock.Setup(x => x.WebRootPath).Returns("/wwwroot");
        mock.Setup(x => x.ContentRootPath).Returns("/");
        Services.AddSingleton(mock.Object);
        return mock;
    }

    /// <summary>
    /// Sets up a mock NavigationManager
    /// </summary>
    protected Mock<Microsoft.AspNetCore.Components.NavigationManager> SetupNavigationManager()
    {
        var mock = new Mock<Microsoft.AspNetCore.Components.NavigationManager>();
        Services.AddSingleton(mock.Object);
        return mock;
    }
}
