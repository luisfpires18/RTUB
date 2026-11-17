// MemberInstrumentService: add and remove.
using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class MemberInstrumentServiceTests
{
    [Fact]
    public async Task AddInstrumentAsync_WithValidInstrument_AddsInstrument()
    {
        // Arrange
        var expectedInstrument = MemberInstrument.Create(_testMemberId, InstrumentType.Guitarra, true);
        var emptyList = new List<MemberInstrument>();
        var mockQueryable = emptyList.BuildMockDbSet().Object;
        _mockRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<MemberInstrument>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));
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
        _mockRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<MemberInstrument>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<MemberInstrument>()))
            .ReturnsAsync(MemberInstrument.Create(_testMemberId, InstrumentType.Bandolim, true));

        // Act
        await _service.AddInstrumentAsync(_testMemberId, InstrumentType.Bandolim, true);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<MemberInstrument>()), Times.AtLeastOnce);
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
}
