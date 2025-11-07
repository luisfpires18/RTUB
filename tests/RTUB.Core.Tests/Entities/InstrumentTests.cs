using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Instrument entity
/// </summary>
public class InstrumentTests
{
    [Fact]
    public void Create_WithValidData_CreatesInstrument()
    {
        // Arrange
        var category = "Guitarra";
        var name = "Guitarra #1";
        var condition = InstrumentCondition.Good;

        // Act
        var result = Instrument.Create(category, name, condition);

        // Assert
        result.Should().NotBeNull();
        result.Category.Should().Be(category);
        result.Name.Should().Be(name);
        result.Condition.Should().Be(condition);
    }

    [Fact]
    public void Create_WithEmptyCategory_ThrowsArgumentException()
    {
        // Arrange
        var category = "";
        var name = "Guitarra #1";
        var condition = InstrumentCondition.Good;

        // Act & Assert
        var act = () => Instrument.Create(category, name, condition);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*categoria*");
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var category = "Guitarra";
        var name = "";
        var condition = InstrumentCondition.Good;

        // Act & Assert
        var act = () => Instrument.Create(category, name, condition);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nome*");
    }

    [Fact]
    public void Create_WithNameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var category = "Guitarra";
        var name = new string('A', 101);
        var condition = InstrumentCondition.Good;

        // Act & Assert
        var act = () => Instrument.Create(category, name, condition);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*100 caracteres*");
    }

    [Fact]
    public void Update_WithValidData_UpdatesInstrument()
    {
        // Arrange
        var instrument = Instrument.Create("Guitarra", "Guitarra #1", InstrumentCondition.Good);
        var newName = "Guitarra #2";
        var newCondition = InstrumentCondition.Excellent;
        var serialNumber = "SN123";
        var brand = "Fender";
        var location = "Storage Room A";

        // Act
        instrument.Update(newName, newCondition, serialNumber, brand, location);

        // Assert
        instrument.Name.Should().Be(newName);
        instrument.Condition.Should().Be(newCondition);
        instrument.SerialNumber.Should().Be(serialNumber);
        instrument.Brand.Should().Be(brand);
        instrument.Location.Should().Be(location);
    }

    [Fact]
    public void Update_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var instrument = Instrument.Create("Guitarra", "Guitarra #1", InstrumentCondition.Good);

        // Act & Assert
        var act = () => instrument.Update("", InstrumentCondition.Good);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nome*");
    }

    [Fact]
    public void UpdateMaintenance_SetsMaintenanceInfo()
    {
        // Arrange
        var instrument = Instrument.Create("Guitarra", "Guitarra #1", InstrumentCondition.Good);
        var notes = "Changed strings";
        var date = DateTime.UtcNow;

        // Act
        instrument.UpdateMaintenance(notes, date);

        // Assert
        instrument.MaintenanceNotes.Should().Be(notes);
        instrument.LastMaintenanceDate.Should().Be(date);
    }

    [Fact]
    public void ImageSrc_ReturnsCorrectPath()
    {
        // Arrange
        var instrument = Instrument.Create("Guitarra", "Guitarra #1", InstrumentCondition.Good);

        // Act
        var imageSrc = instrument.ImageSrc;

        // Assert
        imageSrc.Should().Contain("/api/images/instrument/");
    }
}
