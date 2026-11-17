using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Pins the storage ownership invariant introduced by unit 029's follow-up:
///
/// <b>An environment may only delete or overwrite an object in its own bucket.</b>
///
/// A DEV database cloned from a sanitized production snapshot is full of absolute production R2
/// URLs. Every delete path turns such a URL into an object key and issues it against whatever
/// bucket the current environment points at, so before this change the only thing standing
/// between DEV and a production object was configuration. These tests hold the line in code.
/// </summary>
public partial class StorageOwnershipTests
{
    private const string DevPublicUrl = "https://pub-dev.r2.dev";
    private const string ProdPublicUrl = "https://pub-prod.r2.dev";
    private const string DevBucket = "rtub-dev";

    private const string DevImageUrl = DevPublicUrl + "/images/Staging/album/7_20260101120000.webp";
    private const string ProdImageUrl = ProdPublicUrl + "/images/Production/album/7_20250101120000.webp";
    private const string ForeignImageUrl = "https://cdn.somewhere-else.example/images/Production/album/7.webp";

    #region Fixtures

    private static void VerifyNoDeletes(Mock<IAmazonS3> s3)
    {
        s3.Verify(c => c.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        s3.Verify(c => c.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        s3.Verify(c => c.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IAmazonS3> NewS3()
    {
        var s3 = new Mock<IAmazonS3>(MockBehavior.Loose);

        s3.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

        s3.Setup(c => c.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());

        return s3;
    }

    private static IConfiguration Config(string? publicUrl, string? referencePublicUrl, string bucket)
    {
        var values = new Dictionary<string, string?>
        {
            ["Cloudflare:R2:Bucket"] = bucket,
            ["Cloudflare:R2:PublicUrl"] = publicUrl
        };

        if (referencePublicUrl != null)
        {
            values["Cloudflare:R2:ReferencePublicUrl"] = referencePublicUrl;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(name);
        return environment.Object;
    }

    private static (CloudflareImageStorageService Service, Mock<IAmazonS3> S3) ImageService(
        string environment,
        string? publicUrl = DevPublicUrl,
        string? referencePublicUrl = ProdPublicUrl,
        string bucket = DevBucket)
    {
        var s3 = NewS3();
        var service = new CloudflareImageStorageService(
            s3.Object,
            Config(publicUrl, referencePublicUrl, bucket),
            Environment(environment),
            NullLogger<CloudflareImageStorageService>.Instance);

        return (service, s3);
    }

    private static (CloudflareReceiptStorageService Service, Mock<IAmazonS3> S3) ReceiptService(string environment)
    {
        var s3 = NewS3();
        var service = new CloudflareReceiptStorageService(
            s3.Object,
            Config(DevPublicUrl, ProdPublicUrl, DevBucket),
            Environment(environment),
            NullLogger<CloudflareReceiptStorageService>.Instance);

        return (service, s3);
    }

    private static (CloudflareGalleryMediaStorageService Service, Mock<IAmazonS3> S3) GalleryService(string environment)
    {
        var s3 = NewS3();
        var service = new CloudflareGalleryMediaStorageService(
            s3.Object,
            Config(DevPublicUrl, ProdPublicUrl, DevBucket),
            Environment(environment),
            NullLogger<CloudflareGalleryMediaStorageService>.Instance);

        return (service, s3);
    }

    #endregion
}
