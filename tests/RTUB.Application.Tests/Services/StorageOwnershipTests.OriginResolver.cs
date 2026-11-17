// StorageOwnership: origin resolver.
using FluentAssertions;
using RTUB.Application.Services.Storage;

namespace RTUB.Application.Tests.Services;

public partial class StorageOwnershipTests
{
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
    public void Resolve_NoCurrentOriginConfigured_FailsClosed()
    {
        // A missing PublicUrl must not make everything deletable.
        var resolver = new StorageOriginResolver(currentPublicUrl: null, referencePublicUrl: null);
        resolver.Resolve(DevImageUrl).Should().NotBe(StorageObjectOrigin.CurrentEnvironment);
        resolver.Resolve(DevImageUrl).Should().Be(StorageObjectOrigin.External);
    }

    [Theory]
    [InlineData(DevPublicUrl + "/")]
    [InlineData(DevPublicUrl + "/some/base/path")]
    [InlineData("HTTPS://PUB-DEV.R2.DEV")]
    public void Resolve_ComparesOriginsNotStringPrefixes(string configuredValue)
    {
        var resolver = new StorageOriginResolver(configuredValue, ProdPublicUrl);

        resolver.Resolve(DevImageUrl).Should().Be(StorageObjectOrigin.CurrentEnvironment);
    }

    #endregion
}
