using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Web.Tests.Pages.Media;

/// <summary>
/// Unit tests for Gallery page behavior
/// Testing pagination, filtering, sorting, and media organization
/// </summary>
public partial class GalleryPageTests
{
    #region Pagination Tests

    [Theory]
    [InlineData(15, 100, 1, 15)]  // Page 1 with default 15 items
    [InlineData(15, 100, 2, 15)]  // Page 2
    [InlineData(15, 100, 7, 10)]  // Last page with remaining items
    [InlineData(30, 100, 1, 30)]  // Page 1 with 30 items
    [InlineData(45, 100, 1, 45)]  // Page 1 with 45 items
    [InlineData(60, 100, 1, 60)]  // Page 1 with 60 items
    [InlineData(75, 100, 1, 75)]  // Page 1 with 75 items
    public void CalculatePaginatedMedia_ReturnsCorrectCount(int pageSize, int totalMedia, int page, int expectedCount)
    {
        // Arrange
        var allMedia = GenerateMedia(totalMedia);

        // Act
        var skip = (page - 1) * pageSize;
        var paginatedMedia = allMedia.Skip(skip).Take(pageSize).ToList();

        // Assert
        paginatedMedia.Count.Should().Be(expectedCount,
            $"Page {page} with page size {pageSize} should show {expectedCount} media items out of {totalMedia} total");
    }

    [Fact]
    public void Pagination_FirstPage_ShowsCorrectMedia()
    {
        // Arrange
        var media = GenerateMedia(50);
        var pageSize = 15;
        var currentPage = 1;

        // Act
        var skip = (currentPage - 1) * pageSize;
        var paginatedMedia = media.Skip(skip).Take(pageSize).ToList();

        // Assert
        paginatedMedia.Should().HaveCount(15);
        paginatedMedia[0].Title.Should().Be("Media 1");
    }

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    [InlineData(75)]
    public void PageSizeOptions_AreValid(int pageSize)
    {
        // Arrange
        var validPageSizes = new[] { 15, 30, 45, 60, 75 };

        // Assert
        validPageSizes.Should().Contain(pageSize);
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public void SortMedia_ByMonthDescending_OrdersCorrectly()
    {
        // Arrange
        var media = new List<GalleryMedia>
        {
            CreateMedia(1, "January", 2024, 1, 15),
            CreateMedia(2, "March", 2024, 3, 10),
            CreateMedia(3, "December", 2024, 12, 5),
            CreateMedia(4, "July", 2024, 7, 20),
        };

        // Act
        var sorted = media
            .OrderByDescending(m => m.Month ?? 0)
            .ThenByDescending(m => m.Day ?? 0)
            .ToList();

        // Assert
        sorted[0].Month.Should().Be(12, "December should be first");
        sorted[1].Month.Should().Be(7, "July should be second");
        sorted[2].Month.Should().Be(3, "March should be third");
        sorted[3].Month.Should().Be(1, "January should be last");
    }

    [Fact]
    public void SortMedia_WithNullMonths_PlacedAtBeginning()
    {
        // Arrange
        var media = new List<GalleryMedia>
        {
            CreateMedia(1, "March", 2024, 3, 10),
            CreateMedia(2, "No Month", 2024, null, null),
            CreateMedia(3, "July", 2024, 7, 20),
        };

        // Act
        var sorted = media
            .OrderByDescending(m => m.Month ?? 0) // null becomes 0, goes to beginning in descending order
            .ThenByDescending(m => m.Day ?? 0)
            .ToList();

        // Assert
        // In descending order: 7 > 3 > 0 (null)
        sorted[0].Month.Should().Be(7, "July (7) should be first in descending order");
        sorted[1].Month.Should().Be(3, "March (3) should be second");
        sorted[2].Month.Should().BeNull("Items without month (0) should be last");
        sorted[2].Title.Should().Be("No Month");
    }

    [Fact]
    public void SortMedia_SameMonth_OrdersByDayDescending()
    {
        // Arrange
        var media = new List<GalleryMedia>
        {
            CreateMedia(1, "Early July", 2024, 7, 5),
            CreateMedia(2, "Late July", 2024, 7, 25),
            CreateMedia(3, "Mid July", 2024, 7, 15),
        };

        // Act
        var sorted = media
            .OrderByDescending(m => m.Month ?? 0)
            .ThenByDescending(m => m.Day ?? 0)
            .ToList();

        // Assert
        sorted[0].Day.Should().Be(25, "Latest day should be first");
        sorted[1].Day.Should().Be(15);
        sorted[2].Day.Should().Be(5);
    }

    #endregion

    #region Helper Methods

    private static List<GalleryMedia> GenerateMedia(int count)
    {
        var media = new List<GalleryMedia>();
        for (int i = 1; i <= count; i++)
        {
            media.Add(CreateMedia(i, $"Media {i}", 2024, (byte)(i % 12 + 1), (byte)(i % 28 + 1)));
        }
        return media;
    }

    private static GalleryMedia CreateMedia(int id, string title, int year, byte? month, byte? day)
    {
        return GalleryMedia.Create(
            uploaderId: "user123",
            title: title,
            mediaType: MediaType.Image,
            mediaUrl: $"https://example.com/media/{id}.jpg",
            year: year,
            month: month,
            day: day,
            takenAt: null,
            thumbnailUrl: null
        );
    }

    private static GalleryMedia CreateMediaWithPrivacy(int id, string title, int year, byte? month, byte? day, bool isPrivate)
    {
        return GalleryMedia.Create(
            uploaderId: "user123",
            title: title,
            mediaType: MediaType.Image,
            mediaUrl: $"https://example.com/media/{id}.jpg",
            year: year,
            month: month,
            day: day,
            thumbnailUrl: null,
            takenAt: null,
            isPrivate: isPrivate
        );
    }

    private static GalleryMedia CreateMediaWithId(int id, string title, int year, byte? month, byte? day)
    {
        var media = GalleryMedia.Create(
            uploaderId: "user123",
            title: title,
            mediaType: MediaType.Image,
            mediaUrl: $"https://example.com/media/{id}.jpg",
            year: year,
            month: month,
            day: day,
            takenAt: null,
            thumbnailUrl: null
        );

        // Use reflection to set the Id since it's internal
        var idProperty = typeof(GalleryMedia).GetProperty("Id");
        idProperty?.SetValue(media, id);

        return media;
    }

    private static GalleryMediaPersonTag CreatePersonTag(int mediaId, string userId)
    {
        return GalleryMediaPersonTag.Create(mediaId, userId);
    }

    private static string FormatDate(int year, byte? month, byte? day)
    {
        var monthNames = new Dictionary<int, string>
        {
            { 1, "Janeiro" }, { 2, "Fevereiro" }, { 3, "Março" },
            { 4, "Abril" }, { 5, "Maio" }, { 6, "Junho" },
            { 7, "Julho" }, { 8, "Agosto" }, { 9, "Setembro" },
            { 10, "Outubro" }, { 11, "Novembro" }, { 12, "Dezembro" }
        };

        if (day.HasValue && month.HasValue)
            return $"{day} {monthNames[month.Value]} {year}";

        if (month.HasValue)
            return $"{monthNames[month.Value]} {year}";

        return year.ToString();
    }

    #endregion
}
