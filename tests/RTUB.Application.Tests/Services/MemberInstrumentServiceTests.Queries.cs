// MemberInstrumentService: queries.
using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class MemberInstrumentServiceTests
{
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
        _mockRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<MemberInstrument>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));

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
        _mockRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<MemberInstrument>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));

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
        _mockRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<MemberInstrument>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));

        // Act
        var result = await _service.GetMemberInstrumentsByUserIdsAsync(new[] { "non-existing-1", "non-existing-2" });

        // Assert
        result.Should().BeEmpty();
    }
}
