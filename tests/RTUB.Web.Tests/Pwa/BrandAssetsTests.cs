using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.Pwa;

/// <summary>
/// Task 046: every RTUB brand asset comes from the square artwork (docs/pwa-practices.md → RTUB brand assets). The
/// manifest only names files that exist, at the size it declares; the removed sizes are referenced nowhere.
/// </summary>
public class BrandAssetsTests
{
    private static readonly string Root = GetProjectRoot();
    private static readonly string WebRoot = Path.Combine(Root, "src", "RTUB.Web", "wwwroot");

    private static string Asset(string url) => Path.Combine(WebRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Width and height from a PNG's IHDR chunk.</summary>
    private static (int Width, int Height) PngSize(string path)
    {
        var header = File.ReadAllBytes(path).AsSpan(0, 24);
        header[1..4].SequenceEqual("PNG"u8).Should().BeTrue($"{path} should be a PNG");
        return (BinaryPrimitives.ReadInt32BigEndian(header[16..20]), BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
    }

    [Fact]
    public void ManifestIcons_ExistAtTheirDeclaredSize_WithOneMaskable()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(WebRoot, "manifest.webmanifest")));
        var icons = manifest.RootElement.GetProperty("icons").EnumerateArray().ToList();

        icons.Select(i => i.GetProperty("sizes").GetString()).Should().Contain(["192x192", "512x512"]);
        icons.Should().ContainSingle(i => i.GetProperty("purpose").GetString() == "maskable")
            .Which.GetProperty("src").GetString().Should().Be("/icons/rtub-logo-maskable-512.png", "the normal icon has transparent corners");

        foreach (var icon in icons)
        {
            var src = icon.GetProperty("src").GetString()!;
            File.Exists(Asset(src)).Should().BeTrue($"{src} is in the manifest");
            var (w, h) = PngSize(Asset(src));
            $"{w}x{h}".Should().Be(icon.GetProperty("sizes").GetString(), src);
        }

        var referenced = Regex.Matches(File.ReadAllText(Path.Combine(WebRoot, "manifest.webmanifest")), "\"src\": \"([^\"]+)\"").Select(m => m.Groups[1].Value);
        referenced.Should().OnlyContain(src => File.Exists(Asset(src)), "shortcuts and screenshots too");
    }

    [Fact]
    public void PlatformIcons_AreTheNewSquareSet()
    {
        PngSize(Path.Combine(WebRoot, "apple-touch-icon.png")).Should().Be((180, 180));
        PngSize(Asset("/icons/rtub-logo-maskable-512.png")).Should().Be((512, 512));
        PngSize(Asset("/icons/rtub-badge-96.png")).Should().Be((96, 96));
        new FileInfo(Path.Combine(WebRoot, "favicon.ico")).Length.Should().BeLessThan(32 * 1024, "16 / 32 / 48 only");
        new FileInfo(Asset("/icons/rtub-logo-352.webp")).Length.Should().BeLessThan(64 * 1024, "it is on every cold load (splash)");

        File.ReadAllText(Path.Combine(Root, "src", "RTUB.Web", "portal", "index.html"))
            .Should().Contain("<link rel=\"apple-touch-icon\" sizes=\"180x180\" href=\"/apple-touch-icon.png\" type=\"image/png\" />");
    }

    [Fact]
    public void RemovedLogoSizes_AreReferencedNowhere()
    {
        var removed = new Regex(@"rtub-logo-(16|32|120|152|167|180|256|384)\.png|apple-touch-icon-180x180\.png");
        var shipped = Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(f, @"\.(cs|cshtml|tsx?|html|js|css|json|webmanifest)$"))
            .Where(f => !Regex.IsMatch(f, @"[\\/](bin|obj|node_modules)[\\/]"));

        shipped.Where(f => removed.IsMatch(File.ReadAllText(f))).Should().BeEmpty();
        File.ReadAllText(Path.Combine(WebRoot, "service-worker.js")).Should().NotContain("'rtub-v2.8.0'",
            "images are cache-first: a replaced logo under the same URL needs a new cache version");
    }

    private static string GetProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find project root directory");
    }
}
