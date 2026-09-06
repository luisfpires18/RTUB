using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Services.Storage;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for DatabaseBackupStorageService.
/// </summary>
public class DatabaseBackupStorageServiceTests : IDisposable
{
    private const string Bucket = "rtub-db-backups";

    private readonly Mock<IAmazonS3> _mockS3Client = new();
    private readonly Mock<ILogger<DatabaseBackupStorageService>> _mockLogger = new();
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file)) File.Delete(file);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_WithoutBucket_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = Options.Create(new DatabaseBackupOptions { Bucket = null });

        // Act & Assert
        Action act = () => new DatabaseBackupStorageService(_mockS3Client.Object, options, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*DatabaseBackup:Bucket*");
    }

    [Fact]
    public void Constructor_WithNullS3Client_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new DatabaseBackupStorageService(null!, CreateOptions(), _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task UploadFileAsync_UploadsToTheBackupBucketWithoutPublicAcl()
    {
        // Arrange
        var service = CreateService();
        var file = CreateTempFile("backup contents");
        PutObjectRequest? captured = null;

        _mockS3Client
            .Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

        // Act
        await service.UploadFileAsync("database/incoming.db", file);

        // Assert
        captured.Should().NotBeNull();
        captured!.BucketName.Should().Be(Bucket);
        captured.Key.Should().Be("database/incoming.db");
        captured.FilePath.Should().Be(file);
        // A database dump must never be publicly readable or cached.
        captured.CannedACL.Should().BeNull();
        captured.Headers.CacheControl.Should().Be("no-store");
        // Required for Cloudflare R2 compatibility.
        captured.UseChunkEncoding.Should().BeFalse();
    }

    [Fact]
    public async Task UploadFileAsync_MissingLocalFile_ThrowsAndUploadsNothing()
    {
        // Arrange
        var service = CreateService();
        var missing = Path.Combine(Path.GetTempPath(), $"rtub-missing-{Guid.NewGuid():N}.db");

        // Act
        var act = async () => await service.UploadFileAsync("database/incoming.db", missing);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
        _mockS3Client.Verify(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CopyAsync_CopiesServerSideWithinTheBackupBucket()
    {
        // Arrange
        var service = CreateService();
        CopyObjectRequest? captured = null;

        _mockS3Client
            .Setup(c => c.CopyObjectAsync(It.IsAny<CopyObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CopyObjectRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new CopyObjectResponse());

        // Act
        await service.CopyAsync("database/current.db", "database/previous.db");

        // Assert
        captured.Should().NotBeNull();
        captured!.SourceBucket.Should().Be(Bucket);
        captured.DestinationBucket.Should().Be(Bucket);
        captured.SourceKey.Should().Be("database/current.db");
        captured.DestinationKey.Should().Be("database/previous.db");
    }

    [Fact]
    public async Task ExistsAsync_ObjectMissing_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        _mockS3Client
            .Setup(c => c.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not found") { StatusCode = System.Net.HttpStatusCode.NotFound });

        // Act
        var exists = await service.ExistsAsync("database/current.db");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetLastModifiedUtcAsync_ObjectMissing_ReturnsNull()
    {
        // Arrange - a missing current.db is the first-run case, not an error
        var service = CreateService();
        _mockS3Client
            .Setup(c => c.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not found") { StatusCode = System.Net.HttpStatusCode.NotFound });

        // Act
        var lastModified = await service.GetLastModifiedUtcAsync("database/current.db");

        // Assert
        lastModified.Should().BeNull();
    }

    [Fact]
    public async Task GetLastModifiedUtcAsync_ExistingObject_ReturnsUtcTimestamp()
    {
        // Arrange
        var service = CreateService();
        var stamp = new DateTime(2026, 9, 6, 3, 30, 0, DateTimeKind.Utc);
        _mockS3Client
            .Setup(c => c.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse { LastModified = stamp });

        // Act
        var lastModified = await service.GetLastModifiedUtcAsync("database/current.db");

        // Assert
        lastModified.Should().Be(stamp);
    }

    [Fact]
    public async Task GetSizeAsync_ReturnsContentLength()
    {
        // Arrange
        var service = CreateService();
        _mockS3Client
            .Setup(c => c.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse { ContentLength = 18_874_368 });

        // Act
        var size = await service.GetSizeAsync("database/incoming.db");

        // Assert
        size.Should().Be(18_874_368);
    }

    private static IOptions<DatabaseBackupOptions> CreateOptions()
        => Options.Create(new DatabaseBackupOptions { Bucket = Bucket });

    private DatabaseBackupStorageService CreateService()
        => new(_mockS3Client.Object, CreateOptions(), _mockLogger.Object);

    private string CreateTempFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"rtub-test-{Guid.NewGuid():N}.db");
        File.WriteAllText(path, contents);
        _tempFiles.Add(path);
        return path;
    }
}
