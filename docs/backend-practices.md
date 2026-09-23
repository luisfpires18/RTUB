# Backend Best Practices

This document outlines backend development best practices for the RTUB project, focusing on C#, .NET, Entity Framework Core, and Clean Architecture principles.

## Table of Contents
- [Architecture](#architecture)
- [Code Organization](#code-organization)
- [Entity Framework Core](#entity-framework-core)
- [Services & Business Logic](#services--business-logic)
- [Error Handling](#error-handling)
- [Performance](#performance)
- [Testing](#testing)

## Architecture

### Clean Architecture Layers

```
RTUB.Core/          # Domain entities, enums, interfaces (no dependencies)
RTUB.Application/   # Business logic, services, DTOs, repositories
RTUB.Web/           # Controllers, API endpoints, Blazor pages
```

**Principles:**
- **RTUB.Core**: Pure C# domain models. No external dependencies.
- **RTUB.Application**: Business logic and data access abstractions.
- **RTUB.Web**: Thin controllers/pages that delegate to services.

### Dependency Injection

✅ **Always:**
- Use constructor injection
- Register services in `Program.cs`
- Prefer interfaces over concrete classes

```csharp
public class MyService : IMyService
{
    private readonly IRepository<Entity> _repository;
    private readonly ILogger<MyService> _logger;

    public MyService(
        IRepository<Entity> repository,
        ILogger<MyService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
}
```

❌ **Never:**
- Use `new` for services (except DTOs/entities)
- Create static service instances
- Use service locator pattern

## Code Organization

### File Structure

```
src/RTUB.Application/
├── Services/           # Business logic implementations
├── Interfaces/         # Service contracts
├── Repositories/       # Data access implementations
├── DTOs/              # Data transfer objects
├── Extensions/         # Extension methods
└── Data/              # DbContext, configurations
```

### Naming Conventions

- **Classes**: PascalCase (`UserService`, `MeetingRepository`)
- **Interfaces**: `I` prefix (`IUserService`, `IMeetingRepository`)
- **Private fields**: `_camelCase` (`_repository`, `_logger`)
- **Methods**: PascalCase (`GetUserByIdAsync`, `CreateMeetingAsync`)
- **Async methods**: `Async` suffix (`GetUserAsync`, `SaveAsync`)

### Entity Design

✅ **Always:**
- Use factory methods for entity creation
- Add business methods to entities (e.g., `Lock()`, `Unlock()`)
- Use value objects for complex types
- Add XML documentation to public members

```csharp
public class Activity : BaseEntity
{
    public bool IsLocked { get; set; }

    public void Lock()
    {
        IsLocked = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

❌ **Never:**
- Put business logic in controllers
- Create entities with public parameterless constructors only
- Use anemic domain models (entities with no behavior)

## Entity Framework Core

### Repository Pattern

✅ **Always:**
- Use `IRepository<T>` for data access
- Use `GetByIdOrThrowAsync<T>()` extension for entity retrieval
- Use `AsNoTracking()` for read-only queries
- Use `Include()` and `ThenInclude()` for eager loading

```csharp
var entity = await _repository.GetByIdOrThrowAsync(id);
var entities = await _repository.QueryAsync(q => q
    .Include(e => e.RelatedEntity)
    .ThenInclude(r => r.NestedEntity)
    .ToListAsync());
```

### Query Optimization

✅ **Always:**
- Avoid N+1 queries (use `Include()`)
- Use `AsNoTracking()` for read-only operations
- Batch load related data when possible
- Use projections for DTOs

```csharp
// ✅ Good: Batch load users
var userIds = participations.Select(p => p.UserId).Distinct().ToList();
var users = await _userManager.Users
    .Where(u => userIds.Contains(u.Id))
    .AsNoTracking()
    .ToListAsync();
var userDict = users.ToDictionary(u => u.Id);
```

❌ **Never:**
- Load entities in loops
- Use `.Result` or `.Wait()` (use `await`)
- Query without `AsNoTracking()` for read-only operations

### Migrations

✅ **Always:**
- Create migrations with descriptive names
- Include Designer.cs files
- Test migrations on development database
- Review generated SQL before applying
- Keep every migration compatible with the previous production release (expand/contract).
  An app rollback never touches the schema, so the release before yours must still run
  against it. The rule, and what to do for a destructive change, is in
  `docs/release-and-rollback.md` → *Migrations: the N-1 rule*.

```bash
dotnet ef migrations add AddIsLockedToActivity --project src/RTUB.Web
```

## Services & Business Logic

### Service Design

✅ **Always:**
- Single Responsibility Principle
- Use DTOs for data transfer
- Add XML documentation to public methods
- Handle exceptions appropriately

```csharp
/// <summary>
/// Updates an activity
/// </summary>
/// <param name="id">The ID of the activity to update</param>
/// <param name="name">The new name</param>
/// <exception cref="EntityNotFoundException">Thrown when the activity is not found</exception>
public async Task UpdateActivityAsync(int id, string name)
{
    var activity = await _activityRepository.GetByIdOrThrowAsync(id);
    activity.UpdateDetails(name);
    await _activityRepository.UpdateAsync(activity);
}
```

### Error Handling

✅ **Always:**
- Use `EntityNotFoundException` for missing entities
- Log errors with context
- Return user-friendly error messages
- Use try-catch in service methods

```csharp
try
{
    await _service.DoSomethingAsync();
}
catch (EntityNotFoundException ex)
{
    _logger.LogWarning(ex, "Entity not found: {EntityId}", id);
    throw;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error in {Method}", nameof(DoSomethingAsync));
    throw;
}
```

## Performance

### Caching

✅ **Always:**
- Cache frequently accessed data
- Use `IMemoryCache` for in-memory caching
- Set appropriate expiration times
- Invalidate cache on updates

### Async/Await

✅ **Always:**
- Use `async Task` for all async methods
- Use `await` instead of `.Result` or `.Wait()`
- Configure `CancellationToken` for long-running operations

```csharp
public async Task<Entity> GetEntityAsync(int id, CancellationToken cancellationToken = default)
{
    return await _repository.GetByIdAsync(id);
}
```

## Testing

### Unit Tests

✅ **Always:**
- Test business logic in services
- Mock dependencies
- Test edge cases and error scenarios
- Use descriptive test names

```csharp
[Fact]
public async Task UpdateActivityAsync_WhenActivityNotFound_ThrowsEntityNotFoundException()
{
    // Arrange
    var repository = new Mock<IActivityRepository>();
    repository.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Activity?)null);
    var service = new ActivityService(repository.Object);

    // Act & Assert
    await Assert.ThrowsAsync<EntityNotFoundException>(
        () => service.UpdateActivityAsync(1, "New Name"));
}
```

## Code Quality

### XML Documentation

✅ **Always:**
- Document public classes and methods
- Include parameter descriptions
- Document exceptions
- Add return value descriptions

### Code Review Checklist

- [ ] Follows SOLID principles
- [ ] Uses dependency injection
- [ ] Has XML documentation
- [ ] Handles errors appropriately
- [ ] Uses async/await correctly
- [ ] Avoids N+1 queries
- [ ] Uses `GetByIdOrThrowAsync` for entity retrieval
- [ ] Follows naming conventions

## References

- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)
- [Entity Framework Core Best Practices](https://docs.microsoft.com/en-us/ef/core/performance/)