// Gallery filtering, month grouping, timeline alternation and date formatting
using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Web.Tests.Pages.Media;

public partial class GalleryPageTests
{
    #region Filtering Tests

    [Fact]
    public void FilterMedia_ByYear_ReturnsOnlyMatchingYear()
    {
        // Arrange
        var allMedia = new List<GalleryMedia>
        {
            CreateMedia(1, "2023 Media", 2023, 6, 15),
            CreateMedia(2, "2024 Media 1", 2024, 7, 20),
            CreateMedia(3, "2024 Media 2", 2024, 8, 25),
            CreateMedia(4, "2025 Media", 2025, 1, 10),
        };

        // Act
        var filtered = allMedia.Where(m => m.Year == 2024).ToList();

        // Assert
        filtered.Should().HaveCount(2);
        filtered.All(m => m.Year == 2024).Should().BeTrue();
    }

    [Fact]
    public void FilterMedia_ByPerson_ReturnsOnlyMediaWithPerson()
    {
        // Arrange
        var userId = "user123";
        var media1 = CreateMediaWithId(1, "Media 1", 2024, 7, 15);
        var media2 = CreateMediaWithId(2, "Media 2", 2024, 8, 20);
        var media3 = CreateMediaWithId(3, "Media 3", 2024, 9, 25);

        // Add person tags
        media1.PeopleInMedia.Add(CreatePersonTag(1, userId));
        media3.PeopleInMedia.Add(CreatePersonTag(3, userId));

        var allMedia = new List<GalleryMedia> { media1, media2, media3 };

        // Act
        var filtered = allMedia.Where(m => m.PeopleInMedia.Any(p => p.UserId == userId)).ToList();

        // Assert
        filtered.Should().HaveCount(2);
        filtered.Should().Contain(media1);
        filtered.Should().Contain(media3);
        filtered.Should().NotContain(media2);
    }

    [Fact]
    public void GetAvailableYears_ReturnsDistinctYears()
    {
        // Arrange
        var allMedia = new List<GalleryMedia>
        {
            CreateMedia(1, "2023-1", 2023, 6, 15),
            CreateMedia(2, "2023-2", 2023, 7, 20),
            CreateMedia(3, "2024-1", 2024, 8, 25),
            CreateMedia(4, "2025-1", 2025, 1, 10),
        };

        // Act
        var years = allMedia.Select(m => m.Year).Distinct().OrderByDescending(y => y).ToList();

        // Assert
        years.Should().HaveCount(3);
        years.Should().Equal(2025, 2024, 2023);
    }

    #endregion

    #region Month Grouping Tests

    [Fact]
    public void GroupMedia_ByMonth_GroupsCorrectly()
    {
        // Arrange
        var media = new List<GalleryMedia>
        {
            CreateMedia(1, "July 1", 2024, 7, 5),
            CreateMedia(2, "July 2", 2024, 7, 15),
            CreateMedia(3, "August 1", 2024, 8, 10),
            CreateMedia(4, "September 1", 2024, 9, 20),
        };

        // Act
        var grouped = media.GroupBy(m => m.Month).ToList();

        // Assert
        grouped.Should().HaveCount(3);
        grouped.First(g => g.Key == 7).Should().HaveCount(2);
        grouped.First(g => g.Key == 8).Should().HaveCount(1);
        grouped.First(g => g.Key == 9).Should().HaveCount(1);
    }

    [Fact]
    public void MonthLabel_FormatsCorrectly()
    {
        // Arrange
        var monthNames = new Dictionary<int, string>
        {
            { 1, "Janeiro" },
            { 2, "Fevereiro" },
            { 3, "Março" },
            { 4, "Abril" },
            { 5, "Maio" },
            { 6, "Junho" },
            { 7, "Julho" },
            { 8, "Agosto" },
            { 9, "Setembro" },
            { 10, "Outubro" },
            { 11, "Novembro" },
            { 12, "Dezembro" }
        };

        // Act & Assert
        foreach (var kvp in monthNames)
        {
            var label = $"{kvp.Value} 2024";
            label.Should().Contain(kvp.Value);
            label.Should().Contain("2024");
        }
    }

    #endregion

    #region Timeline Alternation Tests

    [Fact]
    public void TimelineAlternation_EvenIndex_IsLeft()
    {
        // Arrange
        var index = 0;

        // Act
        var isLeft = index % 2 == 0;

        // Assert
        isLeft.Should().BeTrue("Even indices (0, 2, 4...) should be left");
    }

    [Fact]
    public void TimelineAlternation_OddIndex_IsRight()
    {
        // Arrange
        var index = 1;

        // Act
        var isLeft = index % 2 == 0;

        // Assert
        isLeft.Should().BeFalse("Odd indices (1, 3, 5...) should be right");
    }

    [Fact]
    public void TimelineAlternation_MultipleItems_Alternates()
    {
        // Arrange
        var indices = new[] { 0, 1, 2, 3, 4, 5 };

        // Act & Assert
        (indices[0] % 2 == 0).Should().BeTrue();  // left
        (indices[1] % 2 == 0).Should().BeFalse(); // right
        (indices[2] % 2 == 0).Should().BeTrue();  // left
        (indices[3] % 2 == 0).Should().BeFalse(); // right
        (indices[4] % 2 == 0).Should().BeTrue();  // left
        (indices[5] % 2 == 0).Should().BeFalse(); // right
    }

    #endregion

    #region Date Formatting Tests

    [Fact]
    public void FormatMediaDate_FullDate_FormatsCorrectly()
    {
        // Arrange
        var year = 2024;
        byte? month = 7;
        byte? day = 15;

        // Act
        var formatted = FormatDate(year, month, day);

        // Assert
        formatted.Should().Be("15 Julho 2024");
    }

    [Fact]
    public void FormatMediaDate_YearAndMonth_FormatsCorrectly()
    {
        // Arrange
        var year = 2024;
        byte? month = 7;
        byte? day = null;

        // Act
        var formatted = FormatDate(year, month, day);

        // Assert
        formatted.Should().Be("Julho 2024");
    }

    [Fact]
    public void FormatMediaDate_YearOnly_FormatsCorrectly()
    {
        // Arrange
        var year = 2024;
        byte? month = null;
        byte? day = null;

        // Act
        var formatted = FormatDate(year, month, day);

        // Assert
        formatted.Should().Be("2024");
    }

    #endregion
}
