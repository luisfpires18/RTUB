using RTUB.Application.Utilities;
using Xunit;

namespace RTUB.Application.Tests.Utilities;

public class MimeTypeHelperTests
{
    [Theory]
    [InlineData("video.mp4", "video/mp4", "video/mp4")] // Valid content type provided
    [InlineData("video.mp4", "", "video/mp4")] // Empty content type, use extension
    [InlineData("video.mp4", null, "video/mp4")] // Null content type, use extension
    [InlineData("video.mov", "", "video/quicktime")] // MOV file
    [InlineData("video.avi", "", "video/x-msvideo")] // AVI file
    [InlineData("video.webm", "", "video/webm")] // WebM file
    [InlineData("video.mkv", "", "video/x-matroska")] // MKV file
    [InlineData("video.3gp", "", "video/3gpp")] // 3GP file (common mobile format)
    [InlineData("VIDEO.MP4", "", "video/mp4")] // Case insensitive
    [InlineData("myvideo.MP4", "", "video/mp4")] // Case insensitive extension
    [InlineData("unknown.xyz", "", "video/mp4")] // Unknown extension, default to mp4
    [InlineData("noextension", "", "video/mp4")] // No extension, default to mp4
    [InlineData("video.mp4", "application/octet-stream", "video/mp4")] // Wrong type, use extension
    [InlineData("video.mp4", "video/quicktime", "video/quicktime")] // Valid video type, use it
    public void GetVideoMimeType_ShouldReturnCorrectMimeType(string fileName, string? providedContentType, string expectedMimeType)
    {
        // Act
        var result = MimeTypeHelper.GetVideoMimeType(fileName, providedContentType);

        // Assert
        Assert.Equal(expectedMimeType, result);
    }

    [Theory]
    [InlineData("image.jpg", "image/jpeg", "image/jpeg")] // Valid content type provided
    [InlineData("image.jpg", "", "image/jpeg")] // Empty content type, use extension
    [InlineData("image.jpeg", "", "image/jpeg")] // JPEG extension
    [InlineData("image.png", "", "image/png")] // PNG file
    [InlineData("image.gif", "", "image/gif")] // GIF file
    [InlineData("image.webp", "", "image/webp")] // WebP file
    [InlineData("IMAGE.JPG", "", "image/jpeg")] // Case insensitive
    [InlineData("unknown.xyz", "", "image/jpeg")] // Unknown extension, default to jpeg
    [InlineData("image.jpg", "application/octet-stream", "image/jpeg")] // Wrong type, use extension
    public void GetImageMimeType_ShouldReturnCorrectMimeType(string fileName, string? providedContentType, string expectedMimeType)
    {
        // Act
        var result = MimeTypeHelper.GetImageMimeType(fileName, providedContentType);

        // Assert
        Assert.Equal(expectedMimeType, result);
    }

    [Theory]
    [InlineData("video.mp4", "", true, "video/mp4")] // Video file
    [InlineData("image.jpg", "", false, "image/jpeg")] // Image file
    [InlineData("file.mp4", "video/quicktime", true, "video/quicktime")] // Video with valid type
    [InlineData("file.png", "image/png", false, "image/png")] // Image with valid type
    public void GetMediaMimeType_ShouldReturnCorrectMimeTypeBasedOnFlag(string fileName, string? providedContentType, bool isVideo, string expectedMimeType)
    {
        // Act
        var result = MimeTypeHelper.GetMediaMimeType(fileName, providedContentType, isVideo);

        // Assert
        Assert.Equal(expectedMimeType, result);
    }

    [Fact]
    public void GetVideoMimeType_WithMobileUploadScenario_ShouldUseFallback()
    {
        // Arrange - Simulating a mobile phone upload with empty content type
        var fileName = "VID_20231220_123456.mp4"; // Typical mobile video filename
        var providedContentType = ""; // Mobile browsers often provide empty content type

        // Act
        var result = MimeTypeHelper.GetVideoMimeType(fileName, providedContentType);

        // Assert
        Assert.Equal("video/mp4", result);
    }

    [Fact]
    public void GetVideoMimeType_WithiPhoneVideoFormat_ShouldReturnQuicktime()
    {
        // Arrange - iPhone often uses MOV format
        var fileName = "IMG_1234.MOV";
        string? providedContentType = null;

        // Act
        var result = MimeTypeHelper.GetVideoMimeType(fileName, providedContentType);

        // Assert
        Assert.Equal("video/quicktime", result);
    }

    [Fact]
    public void GetVideoMimeType_WithAndroid3GPFormat_ShouldReturn3GPP()
    {
        // Arrange - Some Android phones use 3GP format
        var fileName = "VID_20231220.3gp";
        var providedContentType = "";

        // Act
        var result = MimeTypeHelper.GetVideoMimeType(fileName, providedContentType);

        // Assert
        Assert.Equal("video/3gpp", result);
    }
}
