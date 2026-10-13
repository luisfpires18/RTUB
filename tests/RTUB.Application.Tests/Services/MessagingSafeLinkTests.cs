using FluentAssertions;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

/// <summary>Old system-message links may only open a page of this site (task 041; no open redirect).</summary>
public class MessagingSafeLinkTests
{
    [Theory]
    [InlineData("/events/1", "/events/1")]
    [InlineData("/", "/")]
    [InlineData("https://rtub.example/rehearsals/3?x=1", "/rehearsals/3?x=1")]
    [InlineData("https://evil.example//evil.org/a", null)]
    [InlineData("https://evil.example/\\evil.org", null)]
    [InlineData("//evil.org", null)]
    [InlineData("/\\evil.org", null)]
    [InlineData("/\t/evil.org", null)]
    [InlineData("/\n/evil.org", null)]
    [InlineData("/ok path", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("http://rtub.example/x", null)]
    [InlineData(" ", null)]
    public void SafeLink_KeepsOnlySameSitePaths(string link, string? expected)
    {
        MessagingService.SafeLink(link).Should().Be(expected);
    }
}
