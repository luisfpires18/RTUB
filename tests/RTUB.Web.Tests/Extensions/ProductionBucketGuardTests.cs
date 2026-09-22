using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using RTUB.Web.Extensions;
using Xunit;

namespace RTUB.Web.Tests.Extensions;

/// <summary>
/// The configuration half of the storage ownership model.
///
/// <c>appsettings.json</c> commits <c>Cloudflare:R2:Bucket</c> as the production bucket name, so
/// a non-production environment that does not override it silently inherits production's bucket -
/// and then every upload and every delete it performs is applied to production data. No
/// per-object ownership check can catch that, because the bucket genuinely is the one configured.
/// <c>AddStorageServices</c> refuses to build the container instead.
/// </summary>
public class ProductionBucketGuardTests
{
    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(name);
        return environment.Object;
    }

    private static IConfiguration Config(
        string? bucket,
        string? productionBucket = "rtub",
        bool withCredentials = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cloudflare:R2:Bucket"] = bucket,
            ["Cloudflare:R2:ProductionBucket"] = productionBucket,
            ["Cloudflare:R2:AccessKeyId"] = withCredentials ? "synthetic-key-id" : null,
            ["Cloudflare:R2:SecretAccessKey"] = withCredentials ? "synthetic-secret" : null
        }).Build();

    private static Action Register(
        string environmentName,
        string? bucket,
        string? productionBucket = "rtub",
        bool withCredentials = true) =>
        () => new ServiceCollection().AddStorageServices(
            Config(bucket, productionBucket, withCredentials), Environment(environmentName));

    [Theory]
    [InlineData("Staging")]
    [InlineData("Development")]
    [InlineData("Test")]
    public void NonProductionOnTheProductionBucket_RefusesToStart(string environmentName)
    {
        Register(environmentName, bucket: "rtub").Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*production bucket*");
    }

    [Fact]
    public void ComparisonIgnoresCaseAndSurroundingWhitespace()
    {
        Register("Staging", bucket: "  RTUB ").Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NonProductionOnItsOwnBucket_IsAllowed()
    {
        Register("Staging", bucket: "rtub-dev").Should().NotThrow();
    }

    /// <summary>
    /// Production is never checked: its bucket is supposed to be the production bucket, and the
    /// guard must not be able to stop production starting.
    /// </summary>
    [Fact]
    public void Production_IsNeverChecked()
    {
        Register("Production", bucket: "rtub").Should().NotThrow();
    }

    /// <summary>
    /// With nothing to compare against the guard stays out of the way - a missing bucket is
    /// already a hard failure in BaseCloudflareStorageService, and is not this guard's job.
    /// </summary>
    [Theory]
    [InlineData(null, "rtub")]
    [InlineData("rtub-dev", null)]
    [InlineData(null, null)]
    public void MissingConfiguration_IsNotThisGuardsBusiness(string? bucket, string? productionBucket)
    {
        Register("Staging", bucket, productionBucket).Should().NotThrow();
    }

    /// <summary>
    /// With no R2 credential the S3 client cannot reach any bucket, so there is nothing to refuse.
    /// This is what lets the integration-test host boot on the committed appsettings, and it is a
    /// capability check rather than an allow-list of environment names - anything calling itself
    /// "Test" while holding real credentials is still refused.
    /// </summary>
    [Fact]
    public void NoCredentials_IsNotRefused()
    {
        Register("Test", bucket: "rtub", withCredentials: false).Should().NotThrow();
    }

    [Fact]
    public void CredentialsPresent_IsRefusedWhateverTheEnvironmentIsCalled()
    {
        Register("Test", bucket: "rtub", withCredentials: true).Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*production bucket*");
    }

    /// <summary>
    /// The committed default really is the production bucket name, which is the whole premise of
    /// the guard. If someone changes appsettings.json, this test says so.
    /// </summary>
    [Fact]
    public void CommittedAppSettings_StillPairBucketWithProductionBucket()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepositoryRoot(), "src", "RTUB.Web", "appsettings.json"))
            .Build();

        configuration["Cloudflare:R2:ProductionBucket"].Should().NotBeNullOrWhiteSpace();
        configuration["Cloudflare:R2:ProductionBucket"].Should().Be(configuration["Cloudflare:R2:Bucket"]);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}
