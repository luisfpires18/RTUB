// StorageOwnership: delete rules.
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.Services.Storage;
using Xunit;

namespace RTUB.Application.Tests.Services;

public partial class StorageOwnershipTests
{
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
}
