using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Extensions;

/// <summary>
/// Unit tests for RepositoryExtensions
/// </summary>
public class RepositoryExtensionsTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly IRepository<Event> _repository;

    public RepositoryExtensionsTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _repository = new Application.Repositories.EventRepository(_context);
    }

    [Fact]
    public async Task GetByIdOrThrowAsync_WhenEntityExists_ReturnsEntity()
    {
        // Arrange
        var @event = Event.Create("Test Event", DateTime.UtcNow, "Location", Core.Enums.EventType.Atuacao);
        _context.Events.Add(@event);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdOrThrowAsync(@event.Id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(@event.Id);
        result.Name.Should().Be("Test Event");
    }

    [Fact]
    public async Task GetByIdOrThrowAsync_WhenEntityNotFound_ThrowsEntityNotFoundException()
    {
        // Arrange
        var nonExistentId = 99999;

        // Act
        var act = async () => await _repository.GetByIdOrThrowAsync(nonExistentId);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage($"Event with ID {nonExistentId} not found");
    }

    [Fact]
    public async Task GetByIdOrThrowAsync_WhenEntityNotFound_SetsEntityTypeAndId()
    {
        // Arrange
        var nonExistentId = 99999;

        // Act
        EntityNotFoundException? exception = null;
        try
        {
            await _repository.GetByIdOrThrowAsync(nonExistentId);
        }
        catch (EntityNotFoundException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
        exception!.EntityType.Should().Be("Event");
        exception.EntityId.Should().Be(nonExistentId);
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }
}
