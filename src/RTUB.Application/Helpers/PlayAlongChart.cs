using System.Text.Json;
using System.Text.RegularExpressions;
using RTUB.Application.DTOs;

namespace RTUB.Application.Helpers;

/// <summary>
/// The play-along chart format, version 1 (task 094, docs/react-music.md → Tocar junto). A chart is made
/// offline by tools/playalong from a chord sheet and the song's recording, then uploaded by the Owner. This is the trust boundary: everything a chart may contain is checked here, and what
/// is stored is the chart written back from the typed model, so unknown fields never reach the database.
/// </summary>
public static partial class PlayAlongChart
{
    public const int Version = 1;
    public const int MaxBytes = 512 * 1024;

    private const double MaxDurationSeconds = 3600;
    private const int MaxBeats = 10_000;
    private const int MaxSections = 200;
    private const int MaxLines = 1000;
    private const int MaxWordsPerLine = 60;
    private const int MaxChordsPerLine = 40;

    private static readonly string[] Kinds = { "verse", "refrain", "instrumental" };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // C, F#m7, Bb, Cmaj7, Asus4/B: a root, an optional quality, an optional bass note.
    [GeneratedRegex(@"^[A-G][#b]?[A-Za-z0-9]{0,8}(/[A-G][#b]?)?$")]
    private static partial Regex ChordName();

    public static string Serialize(PlayAlongChartDto chart) => JsonSerializer.Serialize(chart, Json);

    public static PlayAlongChartDto? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PlayAlongChartDto>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Null when the chart is valid; otherwise the first problem, in Portuguese, for the upload form.</summary>
    public static string? Validate(PlayAlongChartDto? chart)
    {
        if (chart is null || chart.Sections is null || chart.Beats is null)
        {
            return "O ficheiro não é uma pauta de aprender.";
        }

        if (chart.Version != Version)
        {
            return $"Versão de pauta desconhecida ({chart.Version}).";
        }

        if (!(chart.Duration > 0 && chart.Duration <= MaxDurationSeconds))
        {
            return "Duração inválida.";
        }

        if (!(chart.Bpm >= 20 && chart.Bpm <= 400))
        {
            return "Andamento inválido.";
        }

        // A chord may sit a little past the measured end of the recording.
        var limit = chart.Duration + 1;
        bool InRange(double t) => t >= 0 && t <= limit;

        if (chart.Beats.Count > MaxBeats || chart.Beats.Any(b => !InRange(b)) || !Ascending(chart.Beats))
        {
            return "Batidas inválidas.";
        }

        if (chart.Sections.Count is 0 or > MaxSections)
        {
            return "A pauta precisa de secções.";
        }

        var lines = 0;
        var chordTimes = new List<double>();
        foreach (var section in chart.Sections)
        {
            if (section?.Lines is null || !Text(section.Name, 60) || !Kinds.Contains(section.Kind)
                || !InRange(section.Start) || !InRange(section.End) || section.End < section.Start)
            {
                return "Secção inválida.";
            }

            foreach (var line in section.Lines)
            {
                if (++lines > MaxLines || line?.Words is null || line.WordTimes is null || line.Chords is null
                    || line.Text is null || line.Text.Length > 300
                    || !InRange(line.Start) || !InRange(line.End) || line.End < line.Start
                    || line.Words.Count > MaxWordsPerLine || line.Words.Count != line.WordTimes.Count
                    || line.Words.Any(w => !Text(w, 60)) || line.WordTimes.Any(t => !InRange(t))
                    || line.Chords.Count > MaxChordsPerLine)
                {
                    return $"Linha inválida na secção \"{section.Name}\".";
                }

                foreach (var chord in line.Chords)
                {
                    if (chord is null || !Text(chord.Label, 16) || chord.Name is null || !ChordName().IsMatch(chord.Name)
                        || !InRange(chord.Time)
                        || (chord.Word is { } word && (word < 0 || word >= line.Words.Count))
                        || (chord.Char is { } at && (chord.Word is null || at < 0 || at >= line.Words[chord.Word.Value].Length)))
                    {
                        return $"Acorde inválido na secção \"{section.Name}\".";
                    }

                    chordTimes.Add(chord.Time);
                }
            }
        }

        if (chordTimes.Count == 0)
        {
            return "A pauta não tem acordes.";
        }

        return Ascending(chordTimes) ? null : "Os acordes não estão por ordem de tempo.";
    }

    private static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;

    private static bool Ascending(IReadOnlyList<double> values)
    {
        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] < values[i - 1])
            {
                return false;
            }
        }

        return true;
    }
}
