using FluentAssertions;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using Xunit;

namespace RTUB.Application.Tests.Helpers;

/// <summary>The play-along chart's trust boundary (task 094): what an uploaded chart may contain.</summary>
public class PlayAlongChartTests
{
    [Fact]
    public void AValidChart_Passes_AndSurvivesTheRoundTrip()
    {
        var chart = Chart();

        PlayAlongChart.Validate(chart).Should().BeNull();
        var back = PlayAlongChart.Deserialize(PlayAlongChart.Serialize(chart));
        back.Should().BeEquivalentTo(chart);
    }

    [Theory]
    [InlineData("C")]
    [InlineData("F#m7")]
    [InlineData("Bb")]
    [InlineData("Cmaj7")]
    [InlineData("Asus4/B")]
    public void ChordNames_InLetters_AreAccepted(string name) =>
        PlayAlongChart.Validate(Chart(chord: new PlayAlongChordDto("X", name, 1, 0, 0))).Should().BeNull();

    [Theory]
    [InlineData("")]
    [InlineData("H")]
    [InlineData("FÁ")]
    [InlineData("<b>")]
    [InlineData("C/")]
    public void OtherChordNames_AreRefused(string name) =>
        PlayAlongChart.Validate(Chart(chord: new PlayAlongChordDto("X", name, 1, 0, 0))).Should().NotBeNull();

    [Fact]
    public void AChordOutsideItsLine_OrOutsideTheRecording_IsRefused()
    {
        PlayAlongChart.Validate(Chart(chord: new PlayAlongChordDto("FÁ", "F", 1, 5, null))).Should().NotBeNull("the line has two words");
        PlayAlongChart.Validate(Chart(chord: new PlayAlongChordDto("FÁ", "F", 1, 0, 9))).Should().NotBeNull("the word has four letters");
        PlayAlongChart.Validate(Chart(chord: new PlayAlongChordDto("FÁ", "F", 1, null, 0))).Should().NotBeNull("a letter needs its word");
        PlayAlongChart.Validate(Chart(chord: new PlayAlongChordDto("FÁ", "F", 99, 0, 0))).Should().NotBeNull("the recording lasts 10 s");
        PlayAlongChart.Validate(Chart(chord: new PlayAlongChordDto("FÁ", "F", -1, 0, 0))).Should().NotBeNull();
    }

    [Fact]
    public void BrokenShapes_AreRefused()
    {
        PlayAlongChart.Validate(null).Should().NotBeNull();
        PlayAlongChart.Validate(Chart() with { Version = 2 }).Should().NotBeNull();
        PlayAlongChart.Validate(Chart() with { Duration = 0 }).Should().NotBeNull();
        PlayAlongChart.Validate(Chart() with { Duration = double.NaN }).Should().NotBeNull();
        PlayAlongChart.Validate(Chart() with { Bpm = 0 }).Should().NotBeNull();
        PlayAlongChart.Validate(Chart() with { Beats = new[] { 2.0, 1.0 } }).Should().NotBeNull("beats go forward");
        PlayAlongChart.Validate(Chart() with { Sections = Array.Empty<PlayAlongSectionDto>() }).Should().NotBeNull();
        PlayAlongChart.Validate(Chart() with { Sections = null! }).Should().NotBeNull("a missing list arrives as null");
        PlayAlongChart.Validate(Chart(kind: "solo")).Should().NotBeNull();
        PlayAlongChart.Validate(Chart(wordTimes: new[] { 1.0 })).Should().NotBeNull("one time per word");
        PlayAlongChart.Deserialize("not json").Should().BeNull();
    }

    [Fact]
    public void ChordTimes_MustGoForward_AcrossTheWholeChart()
    {
        var line = Line(new PlayAlongChordDto("FÁ", "F", 3, 0, 0));
        var earlier = Line(new PlayAlongChordDto("DÓ", "C", 2, 0, 0));
        var chart = Chart() with { Sections = new[] { new PlayAlongSectionDto("Verso 1", "verse", 0, 9, new[] { line, earlier }) } };

        PlayAlongChart.Validate(chart).Should().NotBeNull();
    }

    private static PlayAlongLineDto Line(PlayAlongChordDto chord, double[]? wordTimes = null) =>
        new("Quis ser", 1, 4, new[] { "Quis", "ser" }, wordTimes ?? new[] { 1.0, 2.0 }, new[] { chord });

    private static PlayAlongChartDto Chart(PlayAlongChordDto? chord = null, string kind = "verse", double[]? wordTimes = null) =>
        new(1, 10, 80, new[] { 0.5, 1.25 }, new[]
        {
            new PlayAlongSectionDto("Verso 1", kind, 1, 4, new[] { Line(chord ?? new PlayAlongChordDto("FÁ", "F", 1, 0, 0), wordTimes) }),
        });
}
