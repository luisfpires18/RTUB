using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for InstrumentService - Instrument inventory management
/// MEDIUM-HIGH PRIORITY - Phase 1 Service
/// </summary>
public class InstrumentServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IImageStorageService> _imageStorageServiceMock;
    private readonly InstrumentService _service;

    public InstrumentServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _imageStorageServiceMock = new Mock<IImageStorageService>();
        _service = new InstrumentService(_context, _imageStorageServiceMock.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidInstrument_CreatesInstrument()
    {
        var instrument = Instrument.Create("String", "Acoustic Guitar", InstrumentCondition.Good);
        var result = await _service.CreateAsync(instrument);
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("Acoustic Guitar");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsInstrument()
    {
        var instrument = Instrument.Create("Percussion", "Drum Kit", InstrumentCondition.Excellent);
        _context.Instruments.Add(instrument);
        await _context.SaveChangesAsync();
        
        var result = await _service.GetByIdAsync(instrument.Id);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Drum Kit");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllInstruments_OrderedByName()
    {
        _context.Instruments.AddRange(
            Instrument.Create("String", "Violin", InstrumentCondition.Good),
            Instrument.Create("Wind", "Flute", InstrumentCondition.Excellent),
            Instrument.Create("Brass", "Trumpet", InstrumentCondition.Worn)
        );
        await _context.SaveChangesAsync();
        
        var result = await _service.GetAllAsync();
        result.Should().HaveCount(3);
        result.First().Name.Should().Be("Flute");
    }

    [Fact]
    public async Task GetByCategoryAsync_WithValidCategory_ReturnsInstrumentsOfCategory()
    {
        _context.Instruments.AddRange(
            Instrument.Create("String", "Guitar", InstrumentCondition.Good),
            Instrument.Create("String", "Violin", InstrumentCondition.Excellent),
            Instrument.Create("Wind", "Flute", InstrumentCondition.Good)
        );
        await _context.SaveChangesAsync();
        
        var result = await _service.GetByCategoryAsync("String");
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(i => i.Category.Should().Be("String"));
    }

    [Fact]
    public async Task GetByCategoryAsync_WithNonExistingCategory_ReturnsEmptyList()
    {
        var result = await _service.GetByCategoryAsync("NonExistent");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByConditionAsync_WithValidCondition_ReturnsInstrumentsWithCondition()
    {
        _context.Instruments.AddRange(
            Instrument.Create("String", "Guitar", InstrumentCondition.Excellent),
            Instrument.Create("String", "Violin", InstrumentCondition.Excellent),
            Instrument.Create("Wind", "Flute", InstrumentCondition.Worn)
        );
        await _context.SaveChangesAsync();
        
        var result = await _service.GetByConditionAsync(InstrumentCondition.Excellent);
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(i => i.Condition.Should().Be(InstrumentCondition.Excellent));
    }

    [Fact]
    public async Task GetByLocationAsync_WithValidLocation_ReturnsInstrumentsAtLocation()
    {
        var guitar = Instrument.Create("String", "Guitar", InstrumentCondition.Good);
        guitar.Update("Guitar", InstrumentCondition.Good, location: "Storage A");
        var violin = Instrument.Create("String", "Violin", InstrumentCondition.Excellent);
        violin.Update("Violin", InstrumentCondition.Excellent, location: "Storage A");
        var flute = Instrument.Create("Wind", "Flute", InstrumentCondition.Good);
        flute.Update("Flute", InstrumentCondition.Good, location: "Stage");
        _context.Instruments.AddRange(guitar, violin, flute);
        await _context.SaveChangesAsync();
        
        var result = await _service.GetByLocationAsync("Storage A");
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(i => i.Location.Should().Be("Storage A"));
    }

    [Fact]
    public async Task GetByLocationAsync_WithNonExistingLocation_ReturnsEmptyList()
    {
        var result = await _service.GetByLocationAsync("NonExistent");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WithValidInstrument_UpdatesInstrumentAndInvalidatesCache()
    {
        var instrument = Instrument.Create("String", "Original Guitar", InstrumentCondition.Good);
        _context.Instruments.Add(instrument);
        await _context.SaveChangesAsync();
        
        instrument.Update("Updated Guitar", InstrumentCondition.Excellent);
        await _service.UpdateAsync(instrument);
        
        var updated = await _context.Instruments.FindAsync(instrument.Id);
        updated!.Name.Should().Be("Updated Guitar");
        updated.Condition.Should().Be(InstrumentCondition.Excellent);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingInstrument_DeletesInstrument()
    {
        var instrument = Instrument.Create("String", "To Delete", InstrumentCondition.Good);
        _context.Instruments.Add(instrument);
        await _context.SaveChangesAsync();
        
        await _service.DeleteAsync(instrument.Id);
        var deleted = await _context.Instruments.FindAsync(instrument.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistingInstrument_DoesNotThrow()
    {
        var act = async () => await _service.DeleteAsync(999);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_WithInstrumentHavingImage_DeletesImageFromStorage()
    {
        var instrument = Instrument.Create("String", "Guitar with Image", InstrumentCondition.Good);
        instrument.ImageUrl = "https://example.com/images/guitar.jpg";
        _context.Instruments.Add(instrument);
        await _context.SaveChangesAsync();
        
        await _service.DeleteAsync(instrument.Id);
        
        _imageStorageServiceMock.Verify(x => x.DeleteImageAsync("https://example.com/images/guitar.jpg"), Times.Once);
        var deleted = await _context.Instruments.FindAsync(instrument.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithInstrumentWithoutImage_DoesNotCallImageStorageService()
    {
        var instrument = Instrument.Create("String", "Guitar without Image", InstrumentCondition.Good);
        _context.Instruments.Add(instrument);
        await _context.SaveChangesAsync();
        
        await _service.DeleteAsync(instrument.Id);
        
        _imageStorageServiceMock.Verify(x => x.DeleteImageAsync(It.IsAny<string>()), Times.Never);
        var deleted = await _context.Instruments.FindAsync(instrument.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task GetConditionStatsAsync_ReturnsCorrectGroupedCounts()
    {
        _context.Instruments.AddRange(
            Instrument.Create("String", "Guitar1", InstrumentCondition.Excellent),
            Instrument.Create("String", "Guitar2", InstrumentCondition.Excellent),
            Instrument.Create("String", "Violin", InstrumentCondition.Good),
            Instrument.Create("Wind", "Flute", InstrumentCondition.Worn),
            Instrument.Create("Percussion", "Drum", InstrumentCondition.NeedsMaintenance)
        );
        await _context.SaveChangesAsync();
        
        var result = await _service.GetConditionStatsAsync();
        result.Should().HaveCount(4);
        result[InstrumentCondition.Excellent].Should().Be(2);
        result[InstrumentCondition.Good].Should().Be(1);
        result[InstrumentCondition.Worn].Should().Be(1);
        result[InstrumentCondition.NeedsMaintenance].Should().Be(1);
    }

    [Fact]
    public async Task GetConditionStatsAsync_WithNoInstruments_ReturnsEmptyDictionary()
    {
        var result = await _service.GetConditionStatsAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCategoryStatsAsync_ReturnsCorrectGroupedCounts()
    {
        _context.Instruments.AddRange(
            Instrument.Create("String", "Guitar", InstrumentCondition.Good),
            Instrument.Create("String", "Violin", InstrumentCondition.Excellent),
            Instrument.Create("Wind", "Flute", InstrumentCondition.Good),
            Instrument.Create("Brass", "Trumpet", InstrumentCondition.Worn),
            Instrument.Create("Brass", "Trombone", InstrumentCondition.Good)
        );
        await _context.SaveChangesAsync();
        
        var result = await _service.GetCategoryStatsAsync();
        result.Should().HaveCount(3);
        result["String"].Should().Be(2);
        result["Wind"].Should().Be(1);
        result["Brass"].Should().Be(2);
    }

    [Fact]
    public async Task GetCategoryStatsAsync_WithNoInstruments_ReturnsEmptyDictionary()
    {
        var result = await _service.GetCategoryStatsAsync();
        result.Should().BeEmpty();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
