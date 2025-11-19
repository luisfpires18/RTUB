using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Tests.Fixtures;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MemberInstrumentService - Multiple instruments per member
/// </summary>
public class MemberInstrumentServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly MemberInstrumentService _service;
    private readonly string _testMemberId = "test-member-id";

    public MemberInstrumentServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();
        
        _fixture = fixture;
        _context = _fixture.CreateContext();
        _service = new MemberInstrumentService(_context);
    }

    [Fact]
    public async Task AddInstrumentAsync_WithValidInstrument_AddsInstrument()
    {
        // Act
        var result = await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Guitarra, true);

        // Assert
        result.Should().NotBeNull();
        result.MemberId.Should().Be(_testMemberId);
        result.InstrumentType.Should().Be(InstrumentType.Guitarra);
        result.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task AddInstrumentAsync_WithDuplicateInstrument_ThrowsException()
    {
        // Arrange
        await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Guitarra);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AddInstrumentAsync(_testMemberId, InstrumentType.Guitarra)
        );
    }

    [Fact]
    public async Task AddInstrumentAsync_WithPrimaryFlag_UnmarksOtherPrimaryInstruments()
    {
        // Arrange
        await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Guitarra, true);

        // Act
        await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Bandolim, true);

        // Assert
        var instruments = await _service.GetMemberInstrumentsAsync(_testMemberId);
        instruments.Count(i => i.IsPrimary).Should().Be(1);
        instruments.First(i => i.InstrumentType == InstrumentType.Bandolim).IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task GetMemberInstrumentsAsync_ReturnsInstrumentsOrderedByPrimaryThenType()
    {
        // Arrange
        await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Cavaquinho, false);
        await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Guitarra, true);
        await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Bandolim, false);

        // Act
        var result = await _service.GetMemberInstrumentsAsync(_testMemberId);

        // Assert
        result.Should().HaveCount(3);
        result.First().InstrumentType.Should().Be(InstrumentType.Guitarra);
        result.First().IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task GetPrimaryInstrumentAsync_ReturnsPrimaryInstrument()
    {
        // Arrange
        await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Guitarra, true);
        await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Bandolim, false);

        // Act
        var result = await _service.GetPrimaryInstrumentAsync(_testMemberId);

        // Assert
        result.Should().NotBeNull();
        result!.InstrumentType.Should().Be(InstrumentType.Guitarra);
    }

    [Fact]
    public async Task GetPrimaryInstrumentAsync_WhenNoPrimary_ReturnsNull()
    {
        // Arrange
        await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Guitarra, false);

        // Act
        var result = await _service.GetPrimaryInstrumentAsync(_testMemberId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveInstrumentAsync_RemovesInstrument()
    {
        // Arrange
        var instrument = await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Guitarra);

        // Act
        await _service.RemoveInstrumentAsync(instrument.Id);

        // Assert
        var instruments = await _service.GetMemberInstrumentsAsync(_testMemberId);
        instruments.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveInstrumentAsync_WithNonExistingId_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RemoveInstrumentAsync(999)
        );
    }

    [Fact]
    public async Task SetPrimaryInstrumentAsync_MarksSingleInstrumentAsPrimary()
    {
        // Arrange
        var instrument1 = await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Guitarra, true);
        var instrument2 = await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Bandolim, false);

        // Act
        await _service.SetPrimaryInstrumentAsync(instrument2.Id, _testMemberId);

        // Assert
        var instruments = await _service.GetMemberInstrumentsAsync(_testMemberId);
        instruments.Count(i => i.IsPrimary).Should().Be(1);
        instruments.First(i => i.Id == instrument2.Id).IsPrimary.Should().BeTrue();
        instruments.First(i => i.Id == instrument1.Id).IsPrimary.Should().BeFalse();
    }

    [Fact]
    public async Task SetPrimaryInstrumentAsync_WithNonExistingId_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SetPrimaryInstrumentAsync(999, _testMemberId)
        );
    }

    [Fact]
    public async Task SetPrimaryInstrumentAsync_WithMismatchedMemberId_ThrowsException()
    {
        // Arrange
        var instrument = await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Guitarra);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SetPrimaryInstrumentAsync(instrument.Id, "different-member-id")
        );
    }

    [Fact]
    public async Task HasInstrumentAsync_WhenExists_ReturnsTrue()
    {
        // Arrange
        await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Guitarra);

        // Act
        var result = await _service.HasInstrumentAsync(_testMemberId, InstrumentType.Guitarra);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasInstrumentAsync_WhenNotExists_ReturnsFalse()
    {
        // Act
        var result = await _service.HasInstrumentAsync(_testMemberId, InstrumentType.Guitarra);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsInstrument()
    {
        // Arrange
        var instrument = await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Guitarra);

        // Act
        var result = await _service.GetByIdAsync(instrument.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(instrument.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Act
        var result = await _service.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMemberInstrumentsByUserIdsAsync_ReturnsInstrumentsForMultipleUsers()
    {
        // Arrange
        var user1 = "user-1";
        var user2 = "user-2";
        var user3 = "user-3";
        
        await _service.AddInstrumentAsync(user1, InstrumentType.Guitarra, true);
        await _service.AddInstrumentAsync(user1, InstrumentType.Bandolim, false);
        await _service.AddInstrumentAsync(user2, InstrumentType.Cavaquinho, true);
        await _service.AddInstrumentAsync(user3, InstrumentType.Percussao, false);

        // Act
        var result = await _service.GetMemberInstrumentsByUserIdsAsync(new[] { user1, user2 });

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKey(user1);
        result.Should().ContainKey(user2);
        result.Should().NotContainKey(user3);
        result[user1].Should().HaveCount(2);
        result[user2].Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMemberInstrumentsByUserIdsAsync_WithEmptyList_ReturnsEmptyDictionary()
    {
        // Act
        var result = await _service.GetMemberInstrumentsByUserIdsAsync(new string[] { });

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMemberInstrumentsByUserIdsAsync_WithNonExistingUsers_ReturnsEmptyDictionary()
    {
        // Act
        var result = await _service.GetMemberInstrumentsByUserIdsAsync(new[] { "non-existing-1", "non-existing-2" });

        // Assert
        result.Should().BeEmpty();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
