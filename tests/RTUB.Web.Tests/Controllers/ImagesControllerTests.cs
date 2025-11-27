using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Controllers;

namespace RTUB.Web.Tests.Controllers;

public class ImagesControllerTests : IDisposable
{
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly Mock<ILogger<ImagesController>> _mockLogger;
    private readonly ImagesController _controller;
    private readonly string _testWebRootPath;
    private readonly string _testImagesPath;

    public ImagesControllerTests()
    {
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockLogger = new Mock<ILogger<ImagesController>>();

        // Create a temporary directory for test files
        _testWebRootPath = Path.Combine(Path.GetTempPath(), "ImagesControllerTests_" + Guid.NewGuid().ToString("N"));
        _testImagesPath = Path.Combine(_testWebRootPath, "images");
        Directory.CreateDirectory(_testImagesPath);

        _mockEnvironment.Setup(e => e.WebRootPath).Returns(_testWebRootPath);

        _controller = new ImagesController(_mockEnvironment.Object, _mockLogger.Object);

        // Setup default HTTP context
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose()
    {
        // Cleanup temporary test directory
        if (Directory.Exists(_testWebRootPath))
        {
            try
            {
                Directory.Delete(_testWebRootPath, recursive: true);
            }
            catch (IOException)
            {
                // Ignore IO errors during cleanup (e.g., file in use)
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore permission errors during cleanup
            }
        }
    }

    private void SetupHttpContext(string? ifNoneMatch = null, DateTimeOffset? ifModifiedSince = null)
    {
        var httpContext = new DefaultHttpContext();

        if (ifNoneMatch != null)
        {
            httpContext.Request.Headers.IfNoneMatch = ifNoneMatch;
        }

        if (ifModifiedSince.HasValue)
        {
            httpContext.Request.Headers.IfModifiedSince = ifModifiedSince.Value.ToString("R");
        }

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private string CreateTestFile(string relativePath, string content = "test content")
    {
        var fullPath = Path.Combine(_testImagesPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    #region Path Validation Tests

    [Fact]
    public void GetImage_WithNullPath_ReturnsBadRequest()
    {
        // Arrange
        string? imagePath = null;

        // Act
        var result = _controller.GetImage(imagePath!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Invalid image path");
    }

    [Fact]
    public void GetImage_WithEmptyPath_ReturnsBadRequest()
    {
        // Arrange
        var imagePath = "";

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Invalid image path");
    }

    [Fact]
    public void GetImage_WithWhitespacePath_ReturnsBadRequest()
    {
        // Arrange
        var imagePath = "   ";

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Invalid image path");
    }

    [Fact]
    public void GetImage_WithInvalidPath_ReturnsBadRequest()
    {
        // Arrange - Path with directory traversal attempt
        var imagePath = "../../../etc/passwd";

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Invalid image path");
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../image.png")]
    [InlineData("folder/../image.png")]
    [InlineData("folder/../../image.png")]
    [InlineData("..\\image.png")]
    public void GetImage_WithDirectoryTraversalAttempt_ReturnsBadRequest(string imagePath)
    {
        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Invalid image path");
    }

    [Fact]
    public void GetImage_WithBackslashPath_ReturnsBadRequest()
    {
        // Arrange - Path with backslash (Windows-style separator)
        var imagePath = "folder\\image.png";

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Invalid image path");
    }

    #endregion

    #region File Not Found Tests

    [Fact]
    public void GetImage_WithNonExistentFile_ReturnsNotFound()
    {
        // Arrange
        var imagePath = "nonexistent.png";

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetImage_WithNonExistentSubdirectoryFile_ReturnsNotFound()
    {
        // Arrange
        var imagePath = "subdirectory/nonexistent.png";

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Successful File Return Tests

    [Fact]
    public void GetImage_WithValidFile_ReturnsPhysicalFile()
    {
        // Arrange
        var imagePath = "test.png";
        CreateTestFile(imagePath);

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<PhysicalFileResult>();
        var physicalFileResult = result as PhysicalFileResult;
        physicalFileResult!.ContentType.Should().Be("image/png");
    }

    [Fact]
    public void GetImage_WithSubdirectoryFile_ReturnsPhysicalFile()
    {
        // Arrange
        var imagePath = "hierarchy/test.webp";
        CreateTestFile(imagePath);

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<PhysicalFileResult>();
        var physicalFileResult = result as PhysicalFileResult;
        physicalFileResult!.ContentType.Should().Be("image/webp");
    }

    #endregion

    #region Content Type Detection Tests

    [Theory]
    [InlineData("image.webp", "image/webp")]
    [InlineData("image.png", "image/png")]
    [InlineData("image.jpg", "image/jpeg")]
    [InlineData("image.jpeg", "image/jpeg")]
    [InlineData("image.svg", "image/svg+xml")]
    [InlineData("image.gif", "image/gif")]
    [InlineData("image.unknown", "application/octet-stream")]
    [InlineData("image.bmp", "application/octet-stream")]
    public void GetImage_DeterminesCorrectContentType(string filename, string expectedContentType)
    {
        // Arrange
        CreateTestFile(filename);

        // Act
        var result = _controller.GetImage(filename);

        // Assert
        result.Should().BeOfType<PhysicalFileResult>();
        var physicalFileResult = result as PhysicalFileResult;
        physicalFileResult!.ContentType.Should().Be(expectedContentType);
    }

    [Theory]
    [InlineData("IMAGE.WEBP", "image/webp")]
    [InlineData("IMAGE.PNG", "image/png")]
    [InlineData("IMAGE.JPG", "image/jpeg")]
    [InlineData("IMAGE.JPEG", "image/jpeg")]
    [InlineData("IMAGE.SVG", "image/svg+xml")]
    [InlineData("IMAGE.GIF", "image/gif")]
    public void GetImage_DeterminesCorrectContentType_CaseInsensitive(string filename, string expectedContentType)
    {
        // Arrange
        CreateTestFile(filename);

        // Act
        var result = _controller.GetImage(filename);

        // Assert
        result.Should().BeOfType<PhysicalFileResult>();
        var physicalFileResult = result as PhysicalFileResult;
        physicalFileResult!.ContentType.Should().Be(expectedContentType);
    }

    #endregion

    #region E-Tag Caching Tests

    [Fact]
    public void GetImage_WithMatchingETag_Returns304NotModified()
    {
        // Arrange
        var imagePath = "cached.png";
        var fullPath = CreateTestFile(imagePath);
        var fileInfo = new FileInfo(fullPath);
        var lastModified = fileInfo.LastWriteTimeUtc;
        var expectedETag = $"\"{lastModified.ToFileTime():x}-{fileInfo.Length:x}\"";

        SetupHttpContext(ifNoneMatch: expectedETag);

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<StatusCodeResult>();
        var statusCodeResult = result as StatusCodeResult;
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status304NotModified);
    }

    [Fact]
    public void GetImage_WithNonMatchingETag_ReturnsFile()
    {
        // Arrange
        var imagePath = "cached.png";
        CreateTestFile(imagePath);

        SetupHttpContext(ifNoneMatch: "\"wrong-etag\"");

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<PhysicalFileResult>();
    }

    [Fact]
    public void GetImage_WithWeakETag_ReturnsFile()
    {
        // Arrange - Weak ETags should not match with strong comparison
        var imagePath = "cached.png";
        var fullPath = CreateTestFile(imagePath);
        var fileInfo = new FileInfo(fullPath);
        var lastModified = fileInfo.LastWriteTimeUtc;
        var weakETag = $"W/\"{lastModified.ToFileTime():x}-{fileInfo.Length:x}\"";

        SetupHttpContext(ifNoneMatch: weakETag);

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<PhysicalFileResult>();
    }

    #endregion

    #region If-Modified-Since Tests

    [Fact]
    public void GetImage_WithMatchingIfModifiedSince_Returns304NotModified()
    {
        // Arrange
        var imagePath = "cached.png";
        var fullPath = CreateTestFile(imagePath);
        var fileInfo = new FileInfo(fullPath);

        // Set If-Modified-Since to current time (file was modified before this)
        SetupHttpContext(ifModifiedSince: DateTimeOffset.UtcNow.AddMinutes(1));

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<StatusCodeResult>();
        var statusCodeResult = result as StatusCodeResult;
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status304NotModified);
    }

    [Fact]
    public void GetImage_WithOlderIfModifiedSince_ReturnsFile()
    {
        // Arrange
        var imagePath = "cached.png";
        CreateTestFile(imagePath);

        // Set If-Modified-Since to a time in the past (file was modified after this)
        SetupHttpContext(ifModifiedSince: DateTimeOffset.UtcNow.AddYears(-1));

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<PhysicalFileResult>();
    }

    #endregion

    #region Response Headers Tests

    [Fact]
    public void GetImage_SetsETagHeader()
    {
        // Arrange
        var imagePath = "test.png";
        CreateTestFile(imagePath);

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<PhysicalFileResult>();
        _controller.Response.GetTypedHeaders().ETag.Should().NotBeNull();
    }

    [Fact]
    public void GetImage_SetsLastModifiedHeader()
    {
        // Arrange
        var imagePath = "test.png";
        CreateTestFile(imagePath);

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<PhysicalFileResult>();
        _controller.Response.GetTypedHeaders().LastModified.Should().NotBeNull();
    }

    [Fact]
    public void GetImage_SetsCacheControlHeader()
    {
        // Arrange
        var imagePath = "test.png";
        CreateTestFile(imagePath);

        // Act
        var result = _controller.GetImage(imagePath);

        // Assert
        result.Should().BeOfType<PhysicalFileResult>();
        _controller.Response.Headers.CacheControl.ToString().Should().Be("no-cache");
    }

    #endregion
}
