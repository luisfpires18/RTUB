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
    public void ImageSrc_WithNoImageUrl_ReturnsEmptyString()
    {
        // Arrange
        var instrument = Instrument.Create("Guitarra", "Guitarra #1", InstrumentCondition.Good);

        // Act
        var imageSrc = instrument.ImageSrc;

        // Assert - When no ImageUrl is set, ImageSrc should return empty string
        imageSrc.Should().BeEmpty();
    }

    [Fact]
    public void ImageSrc_WithImageUrl_ReturnsImageUrl()
    {
        // Arrange
        var instrument = Instrument.Create("Guitarra", "Guitarra #1", InstrumentCondition.Good);
        var imageUrl = "https://pub-test.r2.dev/rtub/images/instruments/1.jpg";
        instrument.ImageUrl = imageUrl;

        // Act
        var imageSrc = instrument.ImageSrc;

        // Assert - When ImageUrl is set, ImageSrc should return the ImageUrl
        imageSrc.Should().Be(imageUrl);
    }

    [Fact]
    public void CreateEmpty_CreatesInstrumentWithEmptyFields()
    {
        // Act
        var instrument = Instrument.CreateEmpty();

        // Assert
        instrument.Should().NotBeNull();
        instrument.Category.Should().Be(string.Empty);
        instrument.Name.Should().Be(string.Empty);
        instrument.Condition.Should().Be(InstrumentCondition.Good);
    }

    [Fact]
    public void ThumbnailSrc_WithNoThumbnailUrl_ReturnImageSrc()
    {
        // Arrange
        var instrument = Instrument.Create("Guitarra", "Guitarra #1", InstrumentCondition.Good);
        var imageUrl = "https://pub-test.r2.dev/rtub/images/instruments/1.jpg";
        instrument.ImageUrl = imageUrl;

        // Act
        var thumbnailSrc = instrument.ThumbnailSrc;

        // Assert - When no ThumbnailUrl is set, ThumbnailSrc should return ImageSrc
        thumbnailSrc.Should().Be(imageUrl);
    }

    [Fact]
    public void ThumbnailSrc_WithThumbnailUrl_ReturnsThumbnailUrl()
    {
        // Arrange
        var instrument = Instrument.Create("Guitarra", "Guitarra #1", InstrumentCondition.Good);
        var imageUrl = "https://pub-test.r2.dev/rtub/images/instruments/1.jpg";
        var thumbnailUrl = "https://pub-test.r2.dev/rtub/images/instruments/thumbnails/1.jpg";
        instrument.ImageUrl = imageUrl;
        instrument.ThumbnailUrl = thumbnailUrl;

        // Act
        var thumbnailSrc = instrument.ThumbnailSrc;

        // Assert - When ThumbnailUrl is set, ThumbnailSrc should return the ThumbnailUrl
        thumbnailSrc.Should().Be(thumbnailUrl);
    }

    [Fact]
    public void ThumbnailSrc_WithNoImages_ReturnsEmptyString()
    {
        // Arrange
        var instrument = Instrument.Create("Guitarra", "Guitarra #1", InstrumentCondition.Good);

        // Act
        var thumbnailSrc = instrument.ThumbnailSrc;

        // Assert - When no ImageUrl or ThumbnailUrl is set, ThumbnailSrc should return empty string
        thumbnailSrc.Should().BeEmpty();
    }

    [Fact]
    public void ThumbnailUrl_CanBeSetAndRetrieved()
    {
        // Arrange
        var instrument = Instrument.Create("Guitarra", "Guitarra #1", InstrumentCondition.Good);
        var thumbnailUrl = "https://pub-test.r2.dev/rtub/images/instruments/thumbnails/1.jpg";

        // Act
        instrument.ThumbnailUrl = thumbnailUrl;

        // Assert
        instrument.ThumbnailUrl.Should().Be(thumbnailUrl);
    }
}
