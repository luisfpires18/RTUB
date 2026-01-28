# Testing Best Practices

This document outlines testing best practices for the RTUB project, focusing on unit tests, integration tests, and test organization.

## Table of Contents
- [Test Organization](#test-organization)
- [Unit Testing](#unit-testing)
- [Integration Testing](#integration-testing)
- [Test Patterns](#test-patterns)
- [Mocking](#mocking)
- [Code Coverage](#code-coverage)

**See also:** `docs/tests-structure.md` for project layout, Unit/Integration separation, and test data builders.

## Test Organization

### Project Structure

```
tests/
├── RTUB.Unit.Tests/        # Unit tests for business logic
├── RTUB.Integration.Tests/ # Integration tests for API/UI
```

### Naming Conventions

✅ **Always:**
- Use descriptive test method names
- Follow pattern: `MethodName_StateUnderTest_ExpectedBehavior`
- Use `[Fact]` for xUnit tests
- Group related tests in classes

```csharp
[Fact]
public async Task UpdateActivityAsync_WhenActivityNotFound_ThrowsEntityNotFoundException()
{
    // Test implementation
}
```

## Unit Testing

### AAA Pattern

✅ **Always:**
- Follow Arrange-Act-Assert pattern
- Keep tests focused and isolated
- Test one behavior per test
- Use descriptive variable names

```csharp
[Fact]
public async Task GetByIdOrThrowAsync_WhenEntityExists_ReturnsEntity()
{
    // Arrange
    var entity = new Activity { Id = 1, Name = "Test" };
    var repository = new Mock<IRepository<Activity>>();
    repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(entity);

    // Act
    var result = await repository.Object.GetByIdOrThrowAsync(1);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(1, result.Id);
    Assert.Equal("Test", result.Name);
}
```

### Test Structure

```csharp
public class ActivityServiceTests
{
    private readonly Mock<IActivityRepository> _repositoryMock;
    private readonly ActivityService _service;

    public ActivityServiceTests()
    {
        _repositoryMock = new Mock<IActivityRepository>();
        _service = new ActivityService(_repositoryMock.Object);
    }

    [Fact]
    public async Task UpdateActivityAsync_WhenActivityNotFound_ThrowsEntityNotFoundException()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((Activity?)null);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _service.UpdateActivityAsync(1, "New Name"));
    }
}
```

## Integration Testing

### Test Infrastructure

✅ **Always:**
- Use `TestWebApplicationFactory` for integration tests
- Clean up test data after tests
- Use in-memory database when possible
- Test actual HTTP requests

```csharp
public class MeetingsApiTests : IntegrationTestBase
{
    public MeetingsApiTests(TestWebApplicationFactory factory) 
        : base(factory)
    {
    }

    [Fact]
    public async Task GetMeetings_ReturnsSuccessStatusCode()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/meetings");

        // Assert
        response.EnsureSuccessStatusCode();
    }
}
```

## Test Patterns

### Testing Async Methods

✅ **Always:**
- Use `async Task` for async test methods
- Use `await` in assertions
- Test cancellation tokens when applicable

```csharp
[Fact]
public async Task GetActivityAsync_ReturnsActivity()
{
    // Arrange
    var activity = new Activity { Id = 1 };
    _repositoryMock.Setup(r => r.GetByIdAsync(1))
        .ReturnsAsync(activity);

    // Act
    var result = await _service.GetActivityAsync(1);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(1, result.Id);
}
```

### Testing Exceptions

✅ **Always:**
- Use `Assert.ThrowsAsync<T>` for async exceptions
- Verify exception message when important
- Test exception context

```csharp
[Fact]
public async Task UpdateActivityAsync_WhenActivityNotFound_ThrowsEntityNotFoundException()
{
    // Arrange
    _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
        .ReturnsAsync((Activity?)null);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<EntityNotFoundException>(
        () => _service.UpdateActivityAsync(1, "New Name"));
    
    Assert.Contains("Activity", exception.Message);
}
```

## Mocking

### Mocking Dependencies

✅ **Always:**
- Mock all external dependencies
- Use `Mock<T>` from Moq
- Setup return values explicitly
- Verify method calls when important

```csharp
var repositoryMock = new Mock<IActivityRepository>();
repositoryMock.Setup(r => r.GetByIdAsync(1))
    .ReturnsAsync(new Activity { Id = 1 });

var service = new ActivityService(repositoryMock.Object);

// Verify call was made
repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Activity>()), Times.Once);
```

### Mocking ILogger

✅ **Always:**
- Use `Mock<ILogger<T>>` for logging
- Verify log calls when testing error scenarios

```csharp
var loggerMock = new Mock<ILogger<ActivityService>>();
var service = new ActivityService(repositoryMock.Object, loggerMock.Object);

// Verify error was logged
loggerMock.Verify(
    x => x.Log(
        LogLevel.Error,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => true),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    Times.Once);
```

## Code Coverage

### Coverage Goals

✅ **Always:**
- Aim for 80%+ code coverage
- Focus on business logic coverage
- Test edge cases and error scenarios
- Don't test framework code

### Running Tests with Coverage

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Test Data

### Test Fixtures

✅ **Always:**
- Create test data builders when needed
- Use factories for complex entities
- Keep test data minimal and focused

```csharp
public static class ActivityTestData
{
    public static Activity CreateActivity(int id = 1, string name = "Test Activity")
    {
        return Activity.Create(1, name, DateTime.UtcNow);
    }
}
```

## Best Practices

### Do's

✅ **Always:**
- Write tests before fixing bugs (TDD when possible)
- Keep tests fast and isolated
- Use descriptive test names
- Test behavior, not implementation
- Clean up test data

### Don'ts

❌ **Never:**
- Modify source code to make tests pass
- Delete failing tests (fix them)
- Test private methods directly
- Create expensive test setups
- Share state between tests

## Code Quality

### Test Review Checklist

- [ ] Tests follow AAA pattern
- [ ] Test names are descriptive
- [ ] All dependencies are mocked
- [ ] Edge cases are tested
- [ ] Error scenarios are tested
- [ ] Tests are fast and isolated
- [ ] Test data is minimal

## References

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/)