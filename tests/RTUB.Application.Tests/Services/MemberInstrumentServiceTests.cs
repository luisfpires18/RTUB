using FluentAssertions;
using Moq;
using MockQueryable.Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MemberInstrumentService - Multiple instruments per member
/// </summary>
public class MemberInstrumentServiceTests
{
    private readonly Mock<IMemberInstrumentRepository> _mockRepository;
    private readonly MemberInstrumentService _service;
    private readonly string _testMemberId = "test-member-id";

    public MemberInstrumentServiceTests()
    {
        _mockRepository = new Mock<IMemberInstrumentRepository>();
        _service = new MemberInstrumentService(_mockRepository.Object);
    }

    [Fact]
    public async Task AddInstrumentAsync_WithValidInstrument_AddsInstrument()
    {
        // Arrange
        var expectedInstrument = MemberInstrument.Create(_testMemberId, InstrumentType.Guitarra, true);
        var emptyList = new List<MemberInstrument>();
        var mockQueryable = emptyList.BuildMockDbSet().Object;
        _mockRepository.Setup(r => r.Query()).Returns(mockQueryable);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<MemberInstrument>()))
            .ReturnsAsync(expectedInstrument);
        _mockRepository.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MemberInstrument, bool>>>()))
            .ReturnsAsync(false);

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
        _mockRepository.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MemberInstrument, bool>>>()))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AddInstrumentAsync(_testMemberId, InstrumentType.Guitarra)
        );
    }

    [Fact]
    public async Task AddInstrumentAsync_WithPrimaryFlag_UnmarksOtherPrimaryInstruments()
    {
        // Arrange
        var existingPrimary = MemberInstrument.Create(_testMemberId, InstrumentType.Guitarra, true);
        var instruments = new List<MemberInstrument> { existingPrimary };

        _mockRepository.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MemberInstrument, bool>>>()))
            .ReturnsAsync(false);
        var mockQueryable = instruments.BuildMockDbSet().Object;
        _mockRepository.Setup(r => r.Query()).Returns(mockQueryable);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<MemberInstrument>()))
            .ReturnsAsync(MemberInstrument.Create(_testMemberId, InstrumentType.Bandolim, true));

        // Act
        await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Bandolim, true);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<MemberInstrument>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetMemberInstrumentsAsync_ReturnsInstrumentsOrderedByPrimaryThenType()
    {
        // Arrange - repository returns ordered data
        var instruments = new List<MemberInstrument>
        {
            MemberInstrument.Create(_testMemberId, InstrumentType.Guitarra, true),  // Primary first
            MemberInstrument.Create(_testMemberId, InstrumentType.Bandolim, false),
            MemberInstrument.Create(_testMemberId, InstrumentType.Cavaquinho, false)
        };
        _mockRepository.Setup(r => r.GetByMemberIdAsync(_testMemberId))
            .ReturnsAsync(instruments);

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
        // Arrange - repository returns the primary instrument directly
        var primaryInstrument = MemberInstrument.Create(_testMemberId, InstrumentType.Guitarra, true);
        _mockRepository.Setup(r => r.GetPrimaryInstrumentAsync(_testMemberId))
            .ReturnsAsync(primaryInstrument);

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
        _mockRepository.Setup(r => r.GetPrimaryInstrumentAsync(_testMemberId))
            .ReturnsAsync((MemberInstrument?)null);

        // Act
        var result = await _service.GetPrimaryInstrumentAsync(_testMemberId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveInstrumentAsync_RemovesInstrument()
    {
        // Arrange
        var instrument = MemberInstrument.Create(_testMemberId, InstrumentType.Guitarra, false);
        _mockRepository.Setup(r => r.GetByIdAsync(instrument.Id))
            .ReturnsAsync(instrument);

        // Act
        await _service.RemoveInstrumentAsync(instrument.Id);

        // Assert
        _mockRepository.Verify(r => r.DeleteAsync(It.IsAny<MemberInstrument>()), Times.Once);
    }

    [Fact]
    public async Task RemoveInstrumentAsync_WithNonExistingId_ThrowsException()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((MemberInstrument?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RemoveInstrumentAsync(999)
        );
    }

    [Fact]
    public async Task SetPrimaryInstrumentAsync_MarksSingleInstrumentAsPrimary()
    {
        // Arrange
        var instrument1 = MemberInstrument.Create(_testMemberId, InstrumentType.Guitarra, true);
        var instrument2 = MemberInstrument.Create(_testMemberId, InstrumentType.Bandolim, false);
        var instruments = new List<MemberInstrument> { instrument1, instrument2 };

        var mockQueryable = instruments.BuildMockDbSet().Object;
        _mockRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        await _service.SetPrimaryInstrumentAsync(instrument2.Id, _testMemberId);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<MemberInstrument>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task SetPrimaryInstrumentAsync_WithNonExistingId_ThrowsException()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((MemberInstrument?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SetPrimaryInstrumentAsync(999, _testMemberId)
        );
    }

    [Fact]
    public async Task SetPrimaryInstrumentAsync_WithMismatchedMemberId_ThrowsException()
    {
        // Arrange
        var instrument = MemberInstrument.Create(_testMemberId, InstrumentType.Guitarra, false);
        _mockRepository.Setup(r => r.GetByIdAsync(instrument.Id))
            .ReturnsAsync(instrument);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SetPrimaryInstrumentAsync(instrument.Id, "different-member-id")
        );
    }

    [Fact]
    public async Task HasInstrumentAsync_WhenExists_ReturnsTrue()
    {
        // Arrange
        _mockRepository.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MemberInstrument, bool>>>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.HasInstrumentAsync(_testMemberId, InstrumentType.Guitarra);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasInstrumentAsync_WhenNotExists_ReturnsFalse()
    {
        // Arrange
        _mockRepository.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MemberInstrument, bool>>>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.HasInstrumentAsync(_testMemberId, InstrumentType.Guitarra);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsInstrument()
    {
        // Arrange
        var instrument = MemberInstrument.Create(_testMemberId, InstrumentType.Guitarra, false);
        _mockRepository.Setup(r => r.GetByIdAsync(instrument.Id))
            .ReturnsAsync(instrument);

        // Act
        var result = await _service.GetByIdAsync(instrument.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(instrument.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((MemberInstrument?)null);

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

        var allInstruments = new List<MemberInstrument>
        {
            MemberInstrument.Create(user1, InstrumentType.Guitarra, true),
            MemberInstrument.Create(user1, InstrumentType.Bandolim, false),
            MemberInstrument.Create(user2, InstrumentType.Cavaquinho, true)
        };
        var mockQueryable = allInstruments.BuildMockDbSet().Object;
        _mockRepository.Setup(r => r.Query()).Returns(mockQueryable);

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
        // Arrange
        var emptyList = new List<MemberInstrument>();
        var mockQueryable = emptyList.BuildMockDbSet().Object;
        _mockRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = await _service.GetMemberInstrumentsByUserIdsAsync(new string[] { });

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMemberInstrumentsByUserIdsAsync_WithNonExistingUsers_ReturnsEmptyDictionary()
    {
        // Arrange
        var emptyList = new List<MemberInstrument>();
        var mockQueryable = emptyList.BuildMockDbSet().Object;
        _mockRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = await _service.GetMemberInstrumentsByUserIdsAsync(new[] { "non-existing-1", "non-existing-2" });

        // Assert
        result.Should().BeEmpty();
    }
}
