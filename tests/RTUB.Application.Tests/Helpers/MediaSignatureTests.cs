using System.Text;
using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>Task 135: the type of an upload comes from its first bytes, not from what the browser says.</summary>
public class MediaSignatureTests
{
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "image/gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 9, 9, 9, 9, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D }, "video/mp4")] // ftyp isom
    [InlineData(new byte[] { 0, 0, 0, 0x14, 0x66, 0x74, 0x79, 0x70, 0x71, 0x74, 0x20, 0x20 }, "video/mp4")] // ftyp qt (.mov)
    [InlineData(new byte[] { 0, 0, 0, 8, 0x77, 0x69, 0x64, 0x65 }, "video/mp4")] // an old QuickTime file starts with "wide"
    [InlineData(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }, "video/webm")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 9, 9, 9, 9, 0x57, 0x41, 0x56, 0x45 }, null)] // RIFF, but WAVE
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 }, null)] // %PDF
    [InlineData(new byte[] { }, null)]
    public void Sniff_ReadsTheTypeFromTheFirstBytes(byte[] bytes, string? expected)
    {
        MediaSignature.Sniff(new MemoryStream(bytes)).Should().Be(expected);
    }

    [Fact]
    public void Sniff_DoesNotTakeAnSvgOrAPageForAnImage_AndLeavesTheStreamAtItsStart()
    {
        var svg = new MemoryStream(Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"));
        svg.Position = 5;

        MediaSignature.Sniff(svg).Should().BeNull();
        svg.Position.Should().Be(0);
        MediaSignature.Sniff(new MemoryStream(Encoding.UTF8.GetBytes("<!doctype html><html></html>"))).Should().BeNull();
    }
}
