using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using System.Text.Json;

namespace RTUB.Application.Data;

/// <summary>
/// Seeds learning metadata for existing songs
/// Classifies songs by difficulty, primary instrument, and skill tags
/// </summary>
public static class SongLearningSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        var songs = await context.Songs.ToListAsync();

        if (!songs.Any())
        {
            Console.WriteLine("No songs found to classify for learning platform.");
            return;
        }

        // Dictionary mapping song title keywords to learning metadata
        // Format: keyword → (Difficulty, Primary Instrument, Skill Tags)
        var classifications = new Dictionary<string, (DifficultyLevel, InstrumentType, string[])>
        {
            // Fado songs - typically guitar-based, varying difficulty
            { "fado", (DifficultyLevel.Medium, InstrumentType.Guitarra, new[] { "fingerpicking", "chord-changes", "portuguese-style" }) },
            
            // Corridinho - traditional upbeat dance music
            { "corridinho", (DifficultyLevel.Easy, InstrumentType.Cavaquinho, new[] { "strumming", "rhythm", "fast-tempo" }) },
            
            // Marcha - rhythmic march music
            { "marcha", (DifficultyLevel.Easy, InstrumentType.Percussao, new[] { "rhythm", "tempo", "coordination" }) },
            
            // Vira - traditional dance music
            { "vira", (DifficultyLevel.Easy, InstrumentType.Cavaquinho, new[] { "strumming", "rhythm" }) },
            
            // Chula - folk dance music
            { "chula", (DifficultyLevel.Medium, InstrumentType.Guitarra, new[] { "fingerpicking", "rhythm", "folk-style" }) },
            
            // Malhão - harvest dance
            { "malhão", (DifficultyLevel.Easy, InstrumentType.Bandolim, new[] { "strumming", "tremolo" }) },
            
            // Balada - ballad style
            { "balada", (DifficultyLevel.Medium, InstrumentType.Guitarra, new[] { "fingerpicking", "chord-progression", "slow-tempo" }) },
            
            // Valsa - waltz
            { "valsa", (DifficultyLevel.Medium, InstrumentType.Acordeao, new[] { "waltz-rhythm", "bellows-control" }) },
            
            // Modinha - sentimental song
            { "modinha", (DifficultyLevel.Easy, InstrumentType.Guitarra, new[] { "strumming", "simple-chords" }) },
            
            // Tango - Argentine tango influence
            { "tango", (DifficultyLevel.Hard, InstrumentType.Bandolim, new[] { "tremolo", "fast-passages", "syncopation" }) },
            
            // Traditional Portuguese genres
            { "cantiga", (DifficultyLevel.Easy, InstrumentType.Guitarra, new[] { "folk-style", "strumming" }) },
            { "romanza", (DifficultyLevel.Medium, InstrumentType.Guitarra, new[] { "arpeggios", "melody" }) }
        };

        int classifiedCount = 0;

        foreach (var song in songs)
        {
            // Skip if already classified
            if (song.Difficulty.HasValue)
                continue;

            var titleLower = song.Title.ToLower();
            bool matched = false;

            // Try to match with classification rules
            foreach (var kvp in classifications)
            {
                if (titleLower.Contains(kvp.Key))
                {
                    song.Difficulty = kvp.Value.Item1;
                    song.PrimaryInstrument = kvp.Value.Item2;
                    song.SkillTags = JsonSerializer.Serialize(kvp.Value.Item3);
                    song.EstimatedPracticeHours = kvp.Value.Item1 switch
                    {
                        DifficultyLevel.Easy => 5,
                        DifficultyLevel.Medium => 15,
                        DifficultyLevel.Hard => 40,
                        _ => null
                    };
                    matched = true;
                    classifiedCount++;
                    break;
                }
            }

            // Apply default classification if no match
            if (!matched)
            {
                song.Difficulty = DifficultyLevel.Medium;
                song.PrimaryInstrument = InstrumentType.Guitarra;
                song.SkillTags = JsonSerializer.Serialize(new[] { "portuguese-music" });
                song.EstimatedPracticeHours = 15;
                classifiedCount++;
            }
        }

        if (classifiedCount > 0)
        {
            await context.SaveChangesAsync();
            Console.WriteLine($"Classified {classifiedCount} songs with learning metadata.");
        }
        else
        {
            Console.WriteLine("All songs already have learning metadata.");
        }
    }
}
