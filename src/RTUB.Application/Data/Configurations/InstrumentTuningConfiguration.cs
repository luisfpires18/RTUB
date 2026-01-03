using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using System.Text.Json;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade InstrumentTuning
/// Configura conversão de JSON para SQLite e dados iniciais
/// </summary>
public class InstrumentTuningConfiguration : IEntityTypeConfiguration<InstrumentTuning>
{
    public void Configure(EntityTypeBuilder<InstrumentTuning> builder)
    {
        builder.HasIndex(t => new { t.InstrumentType, t.IsDefault })
            .HasDatabaseName("IX_InstrumentTuning_InstrumentType_IsDefault");

        builder.Property(t => t.TuningNotes)
            .HasConversion(
                v => v,
                v => v
            );

        builder.HasData(GetSeedData());
    }

    private static IEnumerable<InstrumentTuning> GetSeedData()
    {
        return new List<InstrumentTuning>
        {
            new InstrumentTuning
            {
                Id = 1,
                InstrumentType = InstrumentType.Guitarra,
                Name = "Padrão",
                TuningNotes = JsonSerializer.Serialize(new[] { "E", "A", "D", "G", "B", "E" }),
                IsDefault = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "System"
            },
            new InstrumentTuning
            {
                Id = 2,
                InstrumentType = InstrumentType.Bandolim,
                Name = "Padrão",
                TuningNotes = JsonSerializer.Serialize(new[] { "G", "D", "A", "E" }),
                IsDefault = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "System"
            },
            new InstrumentTuning
            {
                Id = 3,
                InstrumentType = InstrumentType.Cavaquinho,
                Name = "Padrão",
                TuningNotes = JsonSerializer.Serialize(new[] { "D", "G", "B", "D" }),
                IsDefault = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "System"
            },
            new InstrumentTuning
            {
                Id = 4,
                InstrumentType = InstrumentType.Baixo,
                Name = "Padrão",
                TuningNotes = JsonSerializer.Serialize(new[] { "E", "A", "D", "G" }),
                IsDefault = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "System"
            },
            new InstrumentTuning
            {
                Id = 5,
                InstrumentType = InstrumentType.Acordeao,
                Name = "Referência",
                TuningNotes = JsonSerializer.Serialize(new[] { "C" }),
                IsDefault = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "System"
            },
            new InstrumentTuning
            {
                Id = 6,
                InstrumentType = InstrumentType.Percussao,
                Name = "Sem Afinação",
                TuningNotes = JsonSerializer.Serialize(Array.Empty<string>()),
                IsDefault = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "System"
            }
        };
    }
}
