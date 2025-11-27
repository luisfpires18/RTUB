using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

public class MemberInstrumentTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var memberId = "member-123";
        var instrumentType = InstrumentType.Guitarra;

        // Act
        var instrument = MemberInstrument.Create(memberId, instrumentType);

        // Assert
        instrument.Should().NotBeNull();
        instrument.MemberId.Should().Be(memberId);
        instrument.InstrumentType.Should().Be(instrumentType);
        instrument.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void Create_WithIsPrimaryTrue_ShouldCreatePrimaryInstrument()
    {
        // Arrange
        var memberId = "member-123";
        var instrumentType = InstrumentType.Bandolim;

        // Act
        var instrument = MemberInstrument.Create(memberId, instrumentType, isPrimary: true);

        // Assert
        instrument.Should().NotBeNull();
        instrument.IsPrimary.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyMemberId_ShouldThrowException(string? memberId)
    {
        // Act
        var act = () => MemberInstrument.Create(memberId!, InstrumentType.Guitarra);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ID*membro*");
    }

    [Theory]
    [InlineData(InstrumentType.Guitarra)]
    [InlineData(InstrumentType.Bandolim)]
    [InlineData(InstrumentType.Cavaquinho)]
    [InlineData(InstrumentType.Acordeao)]
    [InlineData(InstrumentType.Fagote)]
    [InlineData(InstrumentType.Flauta)]
    [InlineData(InstrumentType.Baixo)]
    [InlineData(InstrumentType.Contrabaixo)]
    [InlineData(InstrumentType.Percussao)]
    [InlineData(InstrumentType.Pandeireta)]
    [InlineData(InstrumentType.Estandarte)]
    [InlineData(InstrumentType.Violino)]
    public void Create_WithDifferentInstrumentTypes_ShouldCreateInstance(InstrumentType type)
    {
        // Act
        var instrument = MemberInstrument.Create("member-123", type);

        // Assert
        instrument.Should().NotBeNull();
        instrument.InstrumentType.Should().Be(type);
    }

    #endregion

    #region MarkAsPrimary Tests

    [Fact]
    public void MarkAsPrimary_WhenNotPrimary_ShouldSetIsPrimaryToTrue()
    {
        // Arrange
        var instrument = MemberInstrument.Create("member-123", InstrumentType.Guitarra);

        // Act
        instrument.MarkAsPrimary();

        // Assert
        instrument.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void MarkAsPrimary_WhenAlreadyPrimary_ShouldRemainPrimary()
    {
        // Arrange
        var instrument = MemberInstrument.Create("member-123", InstrumentType.Guitarra, isPrimary: true);

        // Act
        instrument.MarkAsPrimary();

        // Assert
        instrument.IsPrimary.Should().BeTrue();
    }

    #endregion

    #region UnmarkAsPrimary Tests

    [Fact]
    public void UnmarkAsPrimary_WhenPrimary_ShouldSetIsPrimaryToFalse()
    {
        // Arrange
        var instrument = MemberInstrument.Create("member-123", InstrumentType.Guitarra, isPrimary: true);

        // Act
        instrument.UnmarkAsPrimary();

        // Assert
        instrument.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void UnmarkAsPrimary_WhenNotPrimary_ShouldRemainNotPrimary()
    {
        // Arrange
        var instrument = MemberInstrument.Create("member-123", InstrumentType.Guitarra);

        // Act
        instrument.UnmarkAsPrimary();

        // Assert
        instrument.IsPrimary.Should().BeFalse();
    }

    #endregion

    #region Navigation Properties Tests

    [Fact]
    public void Member_NavigationProperty_WhenNotSet_ShouldBeNull()
    {
        // Arrange
        var instrument = MemberInstrument.Create("member-123", InstrumentType.Guitarra);

        // Assert
        instrument.Member.Should().BeNull();
    }

    #endregion

    #region BaseEntity Properties Tests

    [Fact]
    public void BaseEntityProperties_ShouldBeAccessible()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var instrument = MemberInstrument.Create("member-123", InstrumentType.Guitarra);

        // Act
        instrument.CreatedAt = now;
        instrument.CreatedBy = "creator";
        instrument.UpdatedAt = now.AddHours(1);
        instrument.UpdatedBy = "updater";

        // Assert
        instrument.CreatedAt.Should().Be(now);
        instrument.CreatedBy.Should().Be("creator");
        instrument.UpdatedAt.Should().Be(now.AddHours(1));
        instrument.UpdatedBy.Should().Be("updater");
    }

    #endregion
}
