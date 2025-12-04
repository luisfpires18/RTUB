using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RTUB.Web.Controllers;
using System.Net;
using System.Net.Http.Headers;

namespace RTUB.Web.Tests.Controllers;

public class DownloadMediaControllerTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<DownloadMediaController>> _mockLogger;
    private readonly DownloadMediaController _controller;

    public DownloadMediaControllerTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<DownloadMediaController>>();
        _controller = new DownloadMediaController(_mockHttpClientFactory.Object, _mockLogger.Object);

        // Setup default HTTP context
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task Download_WithNullUrl_ReturnsBadRequest()
    {
        // Arrange
        string? url = null;
        var filename = "test.jpg";

        // Act
        var result = await _controller.Download(url!, filename);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("URL is required");
    }

    [Fact]
    public async Task Download_WithEmptyUrl_ReturnsBadRequest()
    {
        // Arrange
        var url = "";
        var filename = "test.jpg";

        // Act
        var result = await _controller.Download(url, filename);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("URL is required");
    }

    [Fact]
    public async Task Download_WithNullFilename_ReturnsBadRequest()
    {
        // Arrange
        var url = "https://example.com/test.jpg";
        string? filename = null;

        // Act
        var result = await _controller.Download(url, filename!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Filename is required");
    }

    [Fact]
    public async Task Download_WithEmptyFilename_ReturnsBadRequest()
    {
        // Arrange
        var url = "https://example.com/test.jpg";
        var filename = "";

        // Act
        var result = await _controller.Download(url, filename);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Filename is required");
    }

    [Fact]
    public async Task Download_WithNonAsciiFilename_SetsProperContentDispositionHeader()
    {
        // Arrange
        var url = "https://example.com/test.jpg";
        var filename = "Festival-Covilhã-2020_03.jpg"; // Contains 'ã' character
        var content = new byte[] { 1, 2, 3, 4, 5 };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(content)
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _controller.Download(url, filename);

        // Assert
        result.Should().BeOfType<FileStreamResult>();
        
        // Verify Content-Disposition header is set and doesn't throw exception with non-ASCII characters
        var contentDisposition = _controller.Response.Headers["Content-Disposition"].ToString();
        contentDisposition.Should().NotBeNullOrEmpty();
        contentDisposition.Should().Contain("attachment");
        
        // The filename should be properly encoded (RFC 5987) with filename* parameter
        // which uses percent-encoding for non-ASCII characters
        contentDisposition.Should().Contain("filename*=utf-8''");
        
        // Should also have a fallback filename for older browsers
        contentDisposition.Should().Contain("filename=");
    }

    [Fact]
    public async Task Download_WithAsciiFilename_SetsProperContentDispositionHeader()
    {
        // Arrange
        var url = "https://example.com/test.jpg";
        var filename = "simple-test-file.jpg";
        var content = new byte[] { 1, 2, 3, 4, 5 };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(content)
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _controller.Download(url, filename);

        // Assert
        result.Should().BeOfType<FileStreamResult>();
        
        // Verify Content-Disposition header is set
        var contentDisposition = _controller.Response.Headers["Content-Disposition"].ToString();
        contentDisposition.Should().NotBeNullOrEmpty();
        contentDisposition.Should().Contain("attachment");
    }

    [Fact]
    public async Task Download_WithSuccessfulResponse_ReturnsFileStream()
    {
        // Arrange
        var url = "https://example.com/test.jpg";
        var filename = "test.jpg";
        var content = new byte[] { 1, 2, 3, 4, 5 };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(content)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") }
                }
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _controller.Download(url, filename);

        // Assert
        result.Should().BeOfType<FileStreamResult>();
        var fileResult = result as FileStreamResult;
        fileResult!.ContentType.Should().Be("image/jpeg");
        fileResult.FileDownloadName.Should().Be(filename);
    }

    [Fact]
    public async Task Download_WithFailedResponse_ReturnsNotFound()
    {
        // Arrange
        var url = "https://example.com/nonexistent.jpg";
        var filename = "test.jpg";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _controller.Download(url, filename);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFound = result as NotFoundObjectResult;
        notFound!.Value.Should().Be("File not found");
    }

    [Fact]
    public async Task Download_WithNoContentType_DefaultsToOctetStream()
    {
        // Arrange
        var url = "https://example.com/test.dat";
        var filename = "test.dat";
        var content = new byte[] { 1, 2, 3, 4, 5 };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(content)
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _controller.Download(url, filename);

        // Assert
        result.Should().BeOfType<FileStreamResult>();
        var fileResult = result as FileStreamResult;
        fileResult!.ContentType.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task Download_WithHttpClientException_ReturnsInternalServerError()
    {
        // Arrange
        var url = "https://example.com/test.jpg";
        var filename = "test.jpg";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _controller.Download(url, filename);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        objectResult.Value.Should().Be("Error downloading file");
    }
}
