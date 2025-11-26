using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Utilities;

/// <summary>
/// Helper methods for creating common mocks in tests
/// Reduces code duplication across test files
/// </summary>
public static class MockHelpers
{
    /// <summary>
    /// Creates a mock UserManager with all required dependencies
    /// This eliminates the need to mock IUserStore in every test
    /// </summary>
    /// <returns>A configured Mock of UserManager for ApplicationUser</returns>
    public static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);
    }
}
