using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Implementação do serviço de afinação de instrumentos
/// </summary>
public class TunerService : ITunerService
{
    private readonly ApplicationDbContext _context;
    private readonly double _referenceFrequency;
    private readonly double _toleranceCents;

    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public TunerService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _referenceFrequency = configuration.GetValue<double>("Tuner:ReferenceFrequency", 440.0);
        _toleranceCents = configuration.GetValue<double>("Tuner:ToleranceCents", 5.0);
    }

    public async Task<IEnumerable<InstrumentTuning>> GetTuningPresetsAsync(InstrumentType instrument, CancellationToken ct = default)
    {
        return await _context.InstrumentTunings
            .Where(t => t.InstrumentType == instrument)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public PitchInfo ValidatePitch(double frequency, double referencePitch = 440)
    {
        if (frequency <= 0)
        {
            throw new ArgumentException("A frequência deve ser maior que zero", nameof(frequency));
        }

        var noteNumber = 12 * Math.Log2(frequency / referencePitch) + 69;
        var roundedNoteNumber = (int)Math.Round(noteNumber);

        var noteName = NoteNames[((roundedNoteNumber % 12) + 12) % 12];
        var octave = (roundedNoteNumber / 12) - 1;

        var targetFrequency = referencePitch * Math.Pow(2, (roundedNoteNumber - 69) / 12.0);
        var centsOffset = 1200 * Math.Log2(frequency / targetFrequency);

        var inTune = Math.Abs(centsOffset) <= _toleranceCents;

        return new PitchInfo
        {
            NoteName = noteName,
            Octave = octave,
            Frequency = frequency,
            CentsOffset = centsOffset,
            InTune = inTune
        };
    }
}
