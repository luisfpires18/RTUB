using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Base class for integration tests that provides a shared custom WebApplicationFactory
/// with isolated in-memory database and mock credentials.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<TestWebApplicationFactory>
{
    protected readonly TestWebApplicationFactory Factory;

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
    }
}
