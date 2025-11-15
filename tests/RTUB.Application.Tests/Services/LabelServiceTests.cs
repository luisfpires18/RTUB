using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for LabelService - Content/label management
/// MEDIUM PRIORITY - Phase 1 Service
/// </summary>
public class LabelServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly LabelService _service;

    public LabelServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext());
        _service = new LabelService(_context);
    }

    [Fact]
    public async Task CreateLabelAsync_WithValidData_CreatesLabel()
    {
        var result = await _service.CreateLabelAsync("about-us", "About Us", "Content");
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Reference.Should().Be("about-us");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetLabelByIdAsync_WithExistingId_ReturnsLabel()
    {
        var label = Label.Create("test-ref", "Test", "Content");
        _context.Labels.Add(label);
        await _context.SaveChangesAsync();
        
        var result = await _service.GetLabelByIdAsync(label.Id);
        result.Should().NotBeNull();
        result!.Reference.Should().Be("test-ref");
    }

    [Fact]
    public async Task GetLabelByIdAsync_WithNonExistingId_ReturnsNull()
    {
        var result = await _service.GetLabelByIdAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLabelByReferenceAsync_WithExistingReference_ReturnsLabel()
    {
        var label = Label.Create("about-us", "About Us", "Content");
        _context.Labels.Add(label);
        await _context.SaveChangesAsync();
        
        var result = await _service.GetLabelByReferenceAsync("about-us");
        result.Should().NotBeNull();
        result!.Title.Should().Be("About Us");
    }

    [Fact]
    public async Task GetLabelByReferenceAsync_WithNonExistingReference_ReturnsNull()
    {
        var result = await _service.GetLabelByReferenceAsync("non-existent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLabelByReferenceAsync_WithInactiveLabel_ReturnsNull()
    {
        var label = Label.Create("inactive-label", "Inactive", "Content");
        label.Deactivate();
        _context.Labels.Add(label);
        await _context.SaveChangesAsync();
        
        var result = await _service.GetLabelByReferenceAsync("inactive-label");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLabelByReferenceAsync_WithActiveLabel_ReturnsLabel()
    {
        var label = Label.Create("active-label", "Active", "Content");
        _context.Labels.Add(label);
        await _context.SaveChangesAsync();
        
        var result = await _service.GetLabelByReferenceAsync("active-label");
        result.Should().NotBeNull();
        result!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllLabelsAsync_ReturnsAllLabels()
    {
        _context.Labels.AddRange(
            Label.Create("ref1", "Label 1", "Content 1"),
            Label.Create("ref2", "Label 2", "Content 2"),
            Label.Create("ref3", "Label 3", "Content 3")
        );
        await _context.SaveChangesAsync();
        
        var result = await _service.GetAllLabelsAsync();
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetActiveLabelsAsync_ReturnsOnlyActiveLabels()
    {
        var active1 = Label.Create("active1", "Active 1", "Content");
        var active2 = Label.Create("active2", "Active 2", "Content");
        var inactive = Label.Create("inactive", "Inactive", "Content");
        inactive.Deactivate();
        _context.Labels.AddRange(active1, active2, inactive);
        await _context.SaveChangesAsync();
        
        var result = await _service.GetActiveLabelsAsync();
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(l => l.IsActive.Should().BeTrue());
    }

    [Fact]
    public async Task UpdateLabelContentAsync_WithValidData_UpdatesContent()
    {
        var label = Label.Create("test-ref", "Original", "Original Content");
        _context.Labels.Add(label);
        await _context.SaveChangesAsync();
        
        await _service.UpdateLabelContentAsync(label.Id, "Updated", "Updated Content");
        
        var updated = await _context.Labels.FindAsync(label.Id);
        updated!.Title.Should().Be("Updated");
        updated.Content.Should().Be("Updated Content");
    }

    [Fact]
    public async Task UpdateLabelContentAsync_WithNonExistingId_ThrowsException()
    {
        var act = async () => await _service.UpdateLabelContentAsync(999, "Title", "Content");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Label with ID 999 not found");
    }

    [Fact]
    public async Task ActivateLabelAsync_WithValidId_ActivatesLabel()
    {
        var label = Label.Create("test-ref", "Test", "Content");
        label.Deactivate();
        _context.Labels.Add(label);
        await _context.SaveChangesAsync();
        
        await _service.ActivateLabelAsync(label.Id);
        
        var activated = await _context.Labels.FindAsync(label.Id);
        activated!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateLabelAsync_WithNonExistingId_ThrowsException()
    {
        var act = async () => await _service.ActivateLabelAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Label with ID 999 not found");
    }

    [Fact]
    public async Task DeactivateLabelAsync_WithValidId_DeactivatesLabel()
    {
        var label = Label.Create("test-ref", "Test", "Content");
        _context.Labels.Add(label);
        await _context.SaveChangesAsync();
        
        await _service.DeactivateLabelAsync(label.Id);
        
        var deactivated = await _context.Labels.FindAsync(label.Id);
        deactivated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateLabelAsync_WithNonExistingId_ThrowsException()
    {
        var act = async () => await _service.DeactivateLabelAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Label with ID 999 not found");
    }

    [Fact]
    public async Task DeleteLabelAsync_WithExistingId_DeletesLabel()
    {
        var label = Label.Create("test-ref", "Test", "Content");
        _context.Labels.Add(label);
        await _context.SaveChangesAsync();
        
        await _service.DeleteLabelAsync(label.Id);
        var deleted = await _context.Labels.FindAsync(label.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteLabelAsync_WithNonExistingId_ThrowsException()
    {
        var act = async () => await _service.DeleteLabelAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Label with ID 999 not found");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
