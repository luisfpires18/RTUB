using RTUB.Application.Utilities;

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
