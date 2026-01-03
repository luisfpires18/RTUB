using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade ChordDiagram
/// Configura índices, conversões de dados e restrições
/// </summary>
public class ChordDiagramConfiguration : IEntityTypeConfiguration<ChordDiagram>
{
    public void Configure(EntityTypeBuilder<ChordDiagram> builder)
    {
        // Índice único para garantir um diagrama de acorde por instrumento e nome
        builder.HasIndex(c => new { c.InstrumentType, c.ChordName })
            .IsUnique()
            .HasDatabaseName("IX_ChordDiagram_InstrumentType_ChordName");

        // Índice para pesquisa por tipo de instrumento e dificuldade
        builder.HasIndex(c => new { c.InstrumentType, c.Difficulty })
            .HasDatabaseName("IX_ChordDiagram_InstrumentType_Difficulty");

        // Configuração da coluna JSON para SQLite
        // SQLite armazena JSON como TEXT, então não é necessária conversão especial
        builder.Property(c => c.FingeringData)
            .HasConversion(
                v => v,
                v => v
            )
            .IsRequired();
    }
}
