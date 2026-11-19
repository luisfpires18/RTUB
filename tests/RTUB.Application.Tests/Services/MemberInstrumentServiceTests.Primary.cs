// MemberInstrumentService: primary.
using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class MemberInstrumentServiceTests
{
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
    public async Task SetPrimaryInstrumentAsync_MarksSingleInstrumentAsPrimary()
    {
        // Arrange
        var instrument1 = MemberInstrument.Create(_testMemberId, InstrumentType.Guitarra, true);
        var instrument2 = MemberInstrument.Create(_testMemberId, InstrumentType.Bandolim, false);
        var instruments = new List<MemberInstrument> { instrument1, instrument2 };

        var mockQueryable = instruments.BuildMockDbSet().Object;
        _mockRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<MemberInstrument>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));

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
}
