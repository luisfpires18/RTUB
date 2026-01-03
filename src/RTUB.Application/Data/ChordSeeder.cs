using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

/// <summary>
/// Seeder para popular o banco de dados com acordes iniciais
/// </summary>
public static class ChordSeeder
{
    /// <summary>
    /// Popula o banco de dados com acordes comuns para diferentes instrumentos
    /// </summary>
    public static async Task SeedChordsAsync(ApplicationDbContext context)
    {
        if (await context.ChordDiagrams.AnyAsync())
        {
            return; // Já existem acordes no banco
        }

        var chords = new List<ChordDiagram>();

        // Acordes para Guitarra
        chords.AddRange(GetGuitarChords());

        // Acordes para Cavaquinho
        chords.AddRange(GetCavaquinhoChords());

        // Acordes para Baixo
        chords.AddRange(GetBaixoChords());

        await context.ChordDiagrams.AddRangeAsync(chords);
        await context.SaveChangesAsync();
    }

    private static List<ChordDiagram> GetGuitarChords()
    {
        return new List<ChordDiagram>
        {
            // C - Dó Maior (Fácil)
            ChordDiagram.Create(
                InstrumentType.Guitarra,
                "C",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 6,
                    Frets = new[] { -1, 3, 2, 0, 1, 0 },
                    Fingers = new[] { 0, 3, 2, 0, 1, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // D - Ré Maior (Fácil)
            ChordDiagram.Create(
                InstrumentType.Guitarra,
                "D",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 6,
                    Frets = new[] { -1, -1, 0, 2, 3, 2 },
                    Fingers = new[] { 0, 0, 0, 1, 3, 2 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // E - Mi Maior (Fácil)
            ChordDiagram.Create(
                InstrumentType.Guitarra,
                "E",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 6,
                    Frets = new[] { 0, 2, 2, 1, 0, 0 },
                    Fingers = new[] { 0, 2, 3, 1, 0, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // F - Fá Maior com pestana (Difícil)
            ChordDiagram.Create(
                InstrumentType.Guitarra,
                "F",
                DifficultyLevel.Hard,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 6,
                    Frets = new[] { 1, 3, 3, 2, 1, 1 },
                    Fingers = new[] { 1, 3, 4, 2, 1, 1 },
                    BaseFret = 1,
                    Barres = new[] { new BarreDto { Fret = 1, FromString = 1, ToStringNumber = 6 } }
                })
            ),

            // G - Sol Maior (Fácil)
            ChordDiagram.Create(
                InstrumentType.Guitarra,
                "G",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 6,
                    Frets = new[] { 3, 2, 0, 0, 0, 3 },
                    Fingers = new[] { 2, 1, 0, 0, 0, 3 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // A - Lá Maior (Fácil)
            ChordDiagram.Create(
                InstrumentType.Guitarra,
                "A",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 6,
                    Frets = new[] { -1, 0, 2, 2, 2, 0 },
                    Fingers = new[] { 0, 0, 1, 2, 3, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // Am - Lá Menor (Fácil)
            ChordDiagram.Create(
                InstrumentType.Guitarra,
                "Am",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 6,
                    Frets = new[] { -1, 0, 2, 2, 1, 0 },
                    Fingers = new[] { 0, 0, 2, 3, 1, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // Dm - Ré Menor (Fácil)
            ChordDiagram.Create(
                InstrumentType.Guitarra,
                "Dm",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 6,
                    Frets = new[] { -1, -1, 0, 2, 3, 1 },
                    Fingers = new[] { 0, 0, 0, 2, 3, 1 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // Em - Mi Menor (Fácil)
            ChordDiagram.Create(
                InstrumentType.Guitarra,
                "Em",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 6,
                    Frets = new[] { 0, 2, 2, 0, 0, 0 },
                    Fingers = new[] { 0, 1, 2, 0, 0, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // G7 - Sol com Sétima (Médio)
            ChordDiagram.Create(
                InstrumentType.Guitarra,
                "G7",
                DifficultyLevel.Medium,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 6,
                    Frets = new[] { 3, 2, 0, 0, 0, 1 },
                    Fingers = new[] { 3, 2, 0, 0, 0, 1 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // C7 - Dó com Sétima (Médio)
            ChordDiagram.Create(
                InstrumentType.Guitarra,
                "C7",
                DifficultyLevel.Medium,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 6,
                    Frets = new[] { -1, 3, 2, 3, 1, 0 },
                    Fingers = new[] { 0, 3, 2, 4, 1, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // Bm - Si Menor com pestana (Difícil)
            ChordDiagram.Create(
                InstrumentType.Guitarra,
                "Bm",
                DifficultyLevel.Hard,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 6,
                    Frets = new[] { -1, 2, 4, 4, 3, 2 },
                    Fingers = new[] { 0, 1, 3, 4, 2, 1 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            )
        };
    }

    private static List<ChordDiagram> GetCavaquinhoChords()
    {
        return new List<ChordDiagram>
        {
            // C - Dó Maior (Fácil)
            ChordDiagram.Create(
                InstrumentType.Cavaquinho,
                "C",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 4,
                    Frets = new[] { 0, 0, 0, 3 },
                    Fingers = new[] { 0, 0, 0, 3 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // D - Ré Maior (Fácil)
            ChordDiagram.Create(
                InstrumentType.Cavaquinho,
                "D",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 4,
                    Frets = new[] { 2, 2, 2, 0 },
                    Fingers = new[] { 1, 2, 3, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // G - Sol Maior (Fácil)
            ChordDiagram.Create(
                InstrumentType.Cavaquinho,
                "G",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 4,
                    Frets = new[] { 0, 2, 3, 2 },
                    Fingers = new[] { 0, 1, 3, 2 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // Am - Lá Menor (Fácil)
            ChordDiagram.Create(
                InstrumentType.Cavaquinho,
                "Am",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 4,
                    Frets = new[] { 2, 0, 0, 0 },
                    Fingers = new[] { 2, 0, 0, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // Em - Mi Menor (Fácil)
            ChordDiagram.Create(
                InstrumentType.Cavaquinho,
                "Em",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 4,
                    Frets = new[] { 0, 4, 3, 2 },
                    Fingers = new[] { 0, 4, 3, 1 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // F - Fá Maior (Médio)
            ChordDiagram.Create(
                InstrumentType.Cavaquinho,
                "F",
                DifficultyLevel.Medium,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 4,
                    Frets = new[] { 2, 0, 1, 0 },
                    Fingers = new[] { 2, 0, 1, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // A - Lá Maior (Médio)
            ChordDiagram.Create(
                InstrumentType.Cavaquinho,
                "A",
                DifficultyLevel.Medium,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 4,
                    Frets = new[] { 2, 1, 0, 0 },
                    Fingers = new[] { 2, 1, 0, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            )
        };
    }

    private static List<ChordDiagram> GetBaixoChords()
    {
        return new List<ChordDiagram>
        {
            // C - Dó (Fácil)
            ChordDiagram.Create(
                InstrumentType.Baixo,
                "C",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 4,
                    Frets = new[] { -1, 3, 5, 5 },
                    Fingers = new[] { 0, 1, 3, 4 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // D - Ré (Fácil)
            ChordDiagram.Create(
                InstrumentType.Baixo,
                "D",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 4,
                    Frets = new[] { -1, 5, 7, 7 },
                    Fingers = new[] { 0, 1, 3, 4 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // E - Mi (Fácil)
            ChordDiagram.Create(
                InstrumentType.Baixo,
                "E",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 4,
                    Frets = new[] { 0, 2, 2, -1 },
                    Fingers = new[] { 0, 1, 2, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // G - Sol (Fácil)
            ChordDiagram.Create(
                InstrumentType.Baixo,
                "G",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 4,
                    Frets = new[] { 3, 5, 5, -1 },
                    Fingers = new[] { 1, 3, 4, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // A - Lá (Fácil)
            ChordDiagram.Create(
                InstrumentType.Baixo,
                "A",
                DifficultyLevel.Easy,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 4,
                    Frets = new[] { 5, 7, 7, -1 },
                    Fingers = new[] { 1, 3, 4, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            ),

            // F - Fá (Médio)
            ChordDiagram.Create(
                InstrumentType.Baixo,
                "F",
                DifficultyLevel.Medium,
                JsonSerializer.Serialize(new ChordFingeringDto
                {
                    Strings = 4,
                    Frets = new[] { 1, 3, 3, -1 },
                    Fingers = new[] { 1, 3, 4, 0 },
                    BaseFret = 1,
                    Barres = Array.Empty<BarreDto>()
                })
            )
        };
    }
}
