// Gallery download file name, privacy and push-notification tagging
using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Web.Tests.Pages.Media;

public partial class GalleryPageTests
{
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
}
