using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for LabelService - Content/label management
/// MEDIUM PRIORITY - Phase 1 Service
/// </summary>
public class LabelServiceTests
{
    private readonly Mock<ILabelRepository> _mockLabelRepository;
    private readonly LabelService _service;

    public LabelServiceTests()
    {
        _mockLabelRepository = new Mock<ILabelRepository>();
        _service = new LabelService(_mockLabelRepository.Object);
    }

    [Fact]
    public async Task CreateLabelAsync_WithValidData_CreatesLabel()
    {
        // Arrange
        var expectedLabel = Label.Create("about-us", "About Us", "Content");
        _mockLabelRepository.Setup(r => r.AddAsync(It.IsAny<Label>()))
            .ReturnsAsync(expectedLabel);

        // Act
        var result = await _service.CreateLabelAsync("about-us", "About Us", "Content");

        // Assert
        result.Should().NotBeNull();
        result.Reference.Should().Be("about-us");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetLabelByIdAsync_WithExistingId_ReturnsLabel()
    {
        // Arrange
        var label = Label.Create("test-ref", "Test", "Content");
        _mockLabelRepository.Setup(r => r.GetByIdAsync(label.Id))
            .ReturnsAsync(label);

        // Act
        var result = await _service.GetLabelByIdAsync(label.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Reference.Should().Be("test-ref");
    }

    [Fact]
    public async Task GetLabelByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Arrange
        _mockLabelRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Label?)null);

        // Act
        var result = await _service.GetLabelByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLabelByReferenceAsync_WithExistingReference_ReturnsLabel()
    {
        // Arrange
        var label = Label.Create("about-us", "About Us", "Content");
        var labelsList = new List<Label> { label };
        var mockQueryable = labelsList.BuildMockDbSet().Object;
        _mockLabelRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = await _service.GetLabelByReferenceAsync("about-us");

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("About Us");
    }

    [Fact]
    public async Task GetLabelByReferenceAsync_WithNonExistingReference_ReturnsNull()
    {
        // Arrange
        var labelsList = new List<Label>();
        var mockQueryable = labelsList.BuildMockDbSet().Object;
        _mockLabelRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = await _service.GetLabelByReferenceAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLabelByReferenceAsync_WithInactiveLabel_ReturnsNull()
    {
        // Arrange
        var label = Label.Create("inactive-label", "Inactive", "Content");
        label.Deactivate();
        var labelsList = new List<Label> { label };
        var mockQueryable = labelsList.BuildMockDbSet().Object;
        _mockLabelRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = await _service.GetLabelByReferenceAsync("inactive-label");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLabelByReferenceAsync_WithActiveLabel_ReturnsLabel()
    {
        // Arrange
        var label = Label.Create("active-label", "Active", "Content");
        var labelsList = new List<Label> { label };
        var mockQueryable = labelsList.BuildMockDbSet().Object;
        _mockLabelRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = await _service.GetLabelByReferenceAsync("active-label");

        // Assert
        result.Should().NotBeNull();
        result!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllLabelsAsync_ReturnsAllLabels()
    {
        // Arrange
        var labelsList = new List<Label>
        {
            Label.Create("ref1", "Label 1", "Content 1"),
            Label.Create("ref2", "Label 2", "Content 2"),
            Label.Create("ref3", "Label 3", "Content 3")
        };
        _mockLabelRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(labelsList);

        // Act
        var result = await _service.GetAllLabelsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetActiveLabelsAsync_ReturnsOnlyActiveLabels()
    {
        // Arrange
        var active1 = Label.Create("active1", "Active 1", "Content");
        var active2 = Label.Create("active2", "Active 2", "Content");
        var inactive = Label.Create("inactive", "Inactive", "Content");
        inactive.Deactivate();
        var labelsList = new List<Label> { active1, active2, inactive };
        var mockQueryable = labelsList.BuildMockDbSet().Object;
        _mockLabelRepository.Setup(r => r.Query()).Returns(mockQueryable);

        // Act
        var result = await _service.GetActiveLabelsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(l => l.IsActive.Should().BeTrue());
    }

    [Fact]
    public async Task UpdateLabelContentAsync_WithValidData_UpdatesContent()
    {
        // Arrange
        var label = Label.Create("test-ref", "Original", "Original Content");
        _mockLabelRepository.Setup(r => r.GetByIdAsync(label.Id))
            .ReturnsAsync(label);

        // Act
        await _service.UpdateLabelContentAsync(label.Id, "Updated", "Updated Content", false);

        // Assert
        label.Title.Should().Be("Updated");
        label.Content.Should().Be("Updated Content");
        label.IsActive.Should().BeFalse();
        _mockLabelRepository.Verify(r => r.UpdateAsync(label), Times.Once);
    }

    [Fact]
    public async Task UpdateLabelContentAsync_WithNonExistingId_ThrowsException()
    {
        // Arrange
        _mockLabelRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Label?)null);

        // Act & Assert
        var act = async () => await _service.UpdateLabelContentAsync(999, "Title", "Content", true);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("Label with ID 999 not found");
    }

    [Fact]
    public async Task ActivateLabelAsync_WithValidId_ActivatesLabel()
    {
        // Arrange
        var label = Label.Create("test-ref", "Test", "Content");
        label.Deactivate();
        _mockLabelRepository.Setup(r => r.GetByIdAsync(label.Id))
            .ReturnsAsync(label);

        // Act
        await _service.ActivateLabelAsync(label.Id);

        // Assert
        label.IsActive.Should().BeTrue();
        _mockLabelRepository.Verify(r => r.UpdateAsync(label), Times.Once);
    }

    [Fact]
    public async Task ActivateLabelAsync_WithNonExistingId_ThrowsException()
    {
        // Arrange
        _mockLabelRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Label?)null);

        // Act & Assert
        var act = async () => await _service.ActivateLabelAsync(999);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("Label with ID 999 not found");
    }

    [Fact]
    public async Task DeactivateLabelAsync_WithValidId_DeactivatesLabel()
    {
        // Arrange
        var label = Label.Create("test-ref", "Test", "Content");
        _mockLabelRepository.Setup(r => r.GetByIdAsync(label.Id))
            .ReturnsAsync(label);

        // Act
        await _service.DeactivateLabelAsync(label.Id);

        // Assert
        label.IsActive.Should().BeFalse();
        _mockLabelRepository.Verify(r => r.UpdateAsync(label), Times.Once);
    }

    [Fact]
    public async Task DeactivateLabelAsync_WithNonExistingId_ThrowsException()
    {
        // Arrange
        _mockLabelRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Label?)null);

        // Act & Assert
        var act = async () => await _service.DeactivateLabelAsync(999);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("Label with ID 999 not found");
    }

    [Fact]
    public async Task DeleteLabelAsync_WithExistingId_DeletesLabel()
    {
        // Arrange
        var label = Label.Create("test-ref", "Test", "Content");
        _mockLabelRepository.Setup(r => r.GetByIdAsync(label.Id))
            .ReturnsAsync(label);

        // Act
        await _service.DeleteLabelAsync(label.Id);

        // Assert
        _mockLabelRepository.Verify(r => r.DeleteAsync(It.IsAny<Label>()), Times.Once);
    }

    [Fact]
    public async Task DeleteLabelAsync_WithNonExistingId_ThrowsException()
    {
        // Arrange
        _mockLabelRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Label?)null);

        // Act & Assert
        var act = async () => await _service.DeleteLabelAsync(999);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("Label with ID 999 not found");
    }
}
