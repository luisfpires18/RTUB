using Xunit;
using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Web.Tests.Pages.Media;

/// <summary>
/// Unit tests for Gallery page behavior
/// Testing pagination, filtering, sorting, and media organization
/// </summary>
public class GalleryPageTests
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

    #region Download Filename Tests

    [Fact]
    public void GetDownloadFileName_ExtractsFromUrl()
    {
        // Arrange
        var mediaUrl = "https://cdn.example.com/images/dev/gallery/image/1733270123-summer-vacation-2024_07_15.jpg";
        var uri = new Uri(mediaUrl);

        // Act
        var fileName = Path.GetFileName(uri.LocalPath);

        // Assert
        fileName.Should().Be("1733270123-summer-vacation-2024_07_15.jpg");
    }

    [Fact]
    public void GetDownloadFileName_FallbackToTitle_WhenUrlInvalid()
    {
        // Arrange
        var title = "Summer Vacation";
        var year = 2024;
        byte? month = 7;
        byte? day = 15;
        var mediaType = MediaType.Image;

        // Act
        var extension = mediaType == MediaType.Image ? ".jpg" : ".mp4";
        var dateStr = FormatDate(year, month, day).Replace(" ", "-");
        var fileName = $"{title}_{dateStr}{extension}";

        // Assert
        fileName.Should().Be("Summer Vacation_15-Julho-2024.jpg");
    }

    #endregion

    #region Privacy Tests

    [Fact]
    public void FilterMedia_AnonymousUser_ReturnsOnlyPublicMedia()
    {
        // Arrange
        var allMedia = new List<GalleryMedia>
        {
            CreateMediaWithPrivacy(1, "Public Media 1", 2024, 7, 15, isPrivate: false),
            CreateMediaWithPrivacy(2, "Private Media", 2024, 8, 20, isPrivate: true),
            CreateMediaWithPrivacy(3, "Public Media 2", 2024, 9, 25, isPrivate: false),
        };

        // Act - simulating anonymous user filter
        var filtered = allMedia.Where(m => !m.IsPrivate).ToList();

        // Assert
        filtered.Should().HaveCount(2);
        filtered.All(m => !m.IsPrivate).Should().BeTrue();
        filtered.Should().Contain(m => m.Title == "Public Media 1");
        filtered.Should().Contain(m => m.Title == "Public Media 2");
        filtered.Should().NotContain(m => m.Title == "Private Media");
    }

    [Fact]
    public void FilterMedia_AuthenticatedUser_ReturnsAllMedia()
    {
        // Arrange
        var allMedia = new List<GalleryMedia>
        {
            CreateMediaWithPrivacy(1, "Public Media", 2024, 7, 15, isPrivate: false),
            CreateMediaWithPrivacy(2, "Private Media", 2024, 8, 20, isPrivate: true),
        };

        // Act - simulating authenticated user (no filter)
        var filtered = allMedia.ToList();

        // Assert
        filtered.Should().HaveCount(2);
        filtered.Should().Contain(m => m.IsPrivate);
        filtered.Should().Contain(m => !m.IsPrivate);
    }

    [Fact]
    public void GetAvailableYears_AnonymousUser_ReturnsOnlyYearsWithPublicMedia()
    {
        // Arrange
        var allMedia = new List<GalleryMedia>
        {
            CreateMediaWithPrivacy(1, "Public 2023", 2023, 6, 15, isPrivate: false),
            CreateMediaWithPrivacy(2, "Private 2023", 2023, 7, 20, isPrivate: true),
            CreateMediaWithPrivacy(3, "Private 2024", 2024, 8, 25, isPrivate: true),
            CreateMediaWithPrivacy(4, "Public 2025", 2025, 1, 10, isPrivate: false),
        };

        // Act - filter for anonymous (public only), then get years
        var publicMedia = allMedia.Where(m => !m.IsPrivate);
        var years = publicMedia.Select(m => m.Year).Distinct().OrderByDescending(y => y).ToList();

        // Assert
        years.Should().HaveCount(2);
        years.Should().Equal(2025, 2023);
        years.Should().NotContain(2024); // 2024 only has private media
    }

    [Fact]
    public void CreateMedia_DefaultIsPrivate_IsTrue()
    {
        // Arrange & Act
        var media = GalleryMedia.Create(
            uploaderId: "user123",
            title: "Test Media",
            mediaType: MediaType.Image,
            mediaUrl: "https://example.com/test.jpg",
            year: 2024,
            month: 7,
            day: 15
        );

        // Assert
        media.IsPrivate.Should().BeTrue("Default privacy should be private (true)");
    }

    [Fact]
    public void CreateMedia_WithIsPrivateFalse_CreatesPublicMedia()
    {
        // Arrange & Act
        var media = GalleryMedia.Create(
            uploaderId: "user123",
            title: "Public Media",
            mediaType: MediaType.Image,
            mediaUrl: "https://example.com/test.jpg",
            year: 2024,
            month: 7,
            day: 15,
            isPrivate: false
        );

        // Assert
        media.IsPrivate.Should().BeFalse("Media should be public");
    }

    [Fact]
    public void UpdatePrivacy_ChangesPrivacySetting()
    {
        // Arrange
        var media = CreateMediaWithPrivacy(1, "Test", 2024, 7, 15, isPrivate: true);

        // Act
        media.UpdatePrivacy(false);

        // Assert
        media.IsPrivate.Should().BeFalse("Privacy should be updated to public");
    }

    [Fact]
    public void UpdatePrivacy_FromPrivateToPublic_Persists()
    {
        // Arrange
        var media = CreateMediaWithPrivacy(1, "Test", 2024, 7, 15, isPrivate: true);
        media.IsPrivate.Should().BeTrue("Initial state should be private");

        // Act
        media.UpdatePrivacy(false);

        // Assert
        media.IsPrivate.Should().BeFalse("Media should now be public after update");
    }

    [Fact]
    public void UpdatePrivacy_FromPublicToPrivate_Persists()
    {
        // Arrange
        var media = CreateMediaWithPrivacy(1, "Test", 2024, 7, 15, isPrivate: false);
        media.IsPrivate.Should().BeFalse("Initial state should be public");

        // Act
        media.UpdatePrivacy(true);

        // Assert
        media.IsPrivate.Should().BeTrue("Media should now be private after update");
    }

    #endregion

    #region Push Notification Tests

    [Fact]
    public void GetTaggedUserIds_FromMedia_ReturnsDistinctUserIds()
    {
        // Arrange
        var media = CreateMediaWithId(1, "Tagged Media", 2024, 7, 15);
        media.PeopleInMedia.Add(CreatePersonTag(1, "user1"));
        media.PeopleInMedia.Add(CreatePersonTag(1, "user2"));
        media.PeopleInMedia.Add(CreatePersonTag(1, "user1")); // duplicate

        // Act
        var taggedUserIds = media.PeopleInMedia
            .Select(p => p.UserId)
            .Distinct()
            .ToList();

        // Assert
        taggedUserIds.Should().HaveCount(2);
        taggedUserIds.Should().Contain("user1");
        taggedUserIds.Should().Contain("user2");
    }

    [Fact]
    public void GetTaggedUserIds_NoTags_ReturnsEmptyList()
    {
        // Arrange
        var media = CreateMediaWithId(1, "Untagged Media", 2024, 7, 15);

        // Act
        var taggedUserIds = media.PeopleInMedia
            .Select(p => p.UserId)
            .Distinct()
            .ToList();

        // Assert
        taggedUserIds.Should().BeEmpty("Media with no tags should return empty list");
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
