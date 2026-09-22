using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.Services;
using RTUB.Application.Services.Storage;
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
public class StorageOwnershipTests
{
    private const string DevPublicUrl = "https://pub-dev.r2.dev";
    private const string ProdPublicUrl = "https://pub-prod.r2.dev";
    private const string DevBucket = "rtub-dev";

    private const string DevImageUrl = DevPublicUrl + "/images/Staging/album/7_20260101120000.webp";
    private const string ProdImageUrl = ProdPublicUrl + "/images/Production/album/7_20250101120000.webp";
    private const string ForeignImageUrl = "https://cdn.somewhere-else.example/images/Production/album/7.webp";

    #region StorageOriginResolver

    [Theory]
    [InlineData(DevImageUrl, StorageObjectOrigin.CurrentEnvironment)]
    [InlineData(ProdImageUrl, StorageObjectOrigin.ProductionReference)]
    [InlineData(ForeignImageUrl, StorageObjectOrigin.External)]
    [InlineData("images/Production/album/7.webp", StorageObjectOrigin.Unknown)]
    [InlineData("", StorageObjectOrigin.Unknown)]
    [InlineData(null, StorageObjectOrigin.Unknown)]
    public void Resolve_ClassifiesByOrigin(string? url, StorageObjectOrigin expected)
    {
        var resolver = new StorageOriginResolver(DevPublicUrl, ProdPublicUrl);

        resolver.Resolve(url).Should().Be(expected);
    }

    [Fact]
    public void Resolve_OnlyCurrentEnvironmentIsDeletable()
    {
        var resolver = new StorageOriginResolver(DevPublicUrl, ProdPublicUrl);

        resolver.IsDeletable(DevImageUrl).Should().BeTrue();
        resolver.IsDeletable(ProdImageUrl).Should().BeFalse();
        resolver.IsDeletable(ForeignImageUrl).Should().BeFalse();
        resolver.IsDeletable("images/Production/x.webp").Should().BeFalse();
    }

    [Fact]
    public void Resolve_NoCurrentOriginConfigured_FailsClosed()
    {
        // A missing PublicUrl must not make everything deletable.
        var resolver = new StorageOriginResolver(currentPublicUrl: null, referencePublicUrl: null);

        resolver.IsDeletable(DevImageUrl).Should().BeFalse();
        resolver.Resolve(DevImageUrl).Should().Be(StorageObjectOrigin.External);
    }

    [Theory]
    [InlineData(DevPublicUrl + "/")]
    [InlineData(DevPublicUrl + "/some/base/path")]
    [InlineData("HTTPS://PUB-DEV.R2.DEV")]
    public void Resolve_ComparesOriginsNotStringPrefixes(string configuredValue)
    {
        var resolver = new StorageOriginResolver(configuredValue, ProdPublicUrl);

        resolver.IsDeletable(DevImageUrl).Should().BeTrue();
    }

    #endregion

    #region DEV: production references are never deleted

    [Fact]
    public async Task DeleteImage_ProductionReferenceInDev_IssuesNoRemoteDelete()
    {
        var (service, s3) = ImageService(environment: "Staging");

        await service.DeleteImageAsync(ProdImageUrl);

        VerifyNoDeletes(s3);
    }

    [Fact]
    public async Task DeleteImage_ForeignUrlInDev_IssuesNoRemoteDelete()
    {
        var (service, s3) = ImageService(environment: "Staging");

        await service.DeleteImageAsync(ForeignImageUrl);

        VerifyNoDeletes(s3);
    }

    [Fact]
    public async Task DeleteImage_UnparseableUrlInDev_FailsClosed()
    {
        var (service, s3) = ImageService(environment: "Staging");

        // A bare key rather than an absolute URL: origin unknown, so it must be refused.
        await service.DeleteImageAsync("images/Production/album/7_20250101120000.webp");

        VerifyNoDeletes(s3);
    }

    [Fact]
    public async Task DeleteImage_DevOwnedUrlInDev_DeletesFromTheDevBucket()
    {
        var (service, s3) = ImageService(environment: "Staging");

        await service.DeleteImageAsync(DevImageUrl);

        s3.Verify(c => c.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r =>
                r.BucketName == DevBucket &&
                r.Key == "images/Staging/album/7_20260101120000.webp"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteReceipt_ProductionReferenceInDev_IssuesNoRemoteDelete()
    {
        var (service, s3) = ReceiptService(environment: "Staging");

        await service.DeleteReceiptAsync(ProdPublicUrl + "/receipts/Production/42_20250101.pdf");

        VerifyNoDeletes(s3);
    }

    [Fact]
    public async Task DeleteGalleryMedia_ProductionReferenceInDev_IssuesNoRemoteDelete()
    {
        // The gallery service used to derive its key with mediaUrl.Replace(publicUrl, ""), which
        // silently produced the whole absolute URL as the key for any foreign origin.
        var (service, s3) = GalleryService(environment: "Staging");

        await service.DeleteMediaAsync(ProdPublicUrl + "/images/Production/gallery/photos/x.webp");

        VerifyNoDeletes(s3);
    }

    #endregion

    #region Replacement: production object untouched, new object written to DEV

    [Fact]
    public async Task ReplacingAProductionBackedImageInDev_UploadsToDevAndDeletesNothing()
    {
        var (service, s3) = ImageService(environment: "Staging");

        // What a profile-picture replacement does: upload the new file, then delete the old one.
        var newUrl = await service.UploadImageAsync(
            new MemoryStream([1, 2, 3]), "avatar.webp", "image/webp", "member", "42");

        await service.DeleteImageAsync(ProdImageUrl);

        // 1. the production object is untouched
        VerifyNoDeletes(s3);

        // 2. the replacement went to the DEV bucket
        s3.Verify(c => c.PutObjectAsync(
            It.Is<PutObjectRequest>(r => r.BucketName == DevBucket && r.Key.StartsWith("images/Staging/")),
            It.IsAny<CancellationToken>()), Times.Once);

        // 3. the URL the database will now hold is the DEV public URL
        newUrl.Should().StartWith(DevPublicUrl + "/images/Staging/member/42");
    }

    #endregion

    #region Production behaviour is unchanged

    [Fact]
    public async Task DeleteImage_OwnUrlInProduction_StillDeletes()
    {
        // Production configures no reference origin at all; its own URLs resolve to
        // CurrentEnvironment and behave exactly as they did before the ownership check.
        var (service, s3) = ImageService(
            environment: "Production",
            publicUrl: ProdPublicUrl,
            referencePublicUrl: null,
            bucket: "rtub");

        await service.DeleteImageAsync(ProdImageUrl);

        s3.Verify(c => c.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r =>
                r.BucketName == "rtub" &&
                r.Key == "images/Production/album/7_20250101120000.webp"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteImage_ForeignUrlInProduction_IsAlsoRefused()
    {
        var (service, s3) = ImageService(
            environment: "Production",
            publicUrl: ProdPublicUrl,
            referencePublicUrl: null,
            bucket: "rtub");

        await service.DeleteImageAsync(ForeignImageUrl);

        VerifyNoDeletes(s3);
    }

    #endregion

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
