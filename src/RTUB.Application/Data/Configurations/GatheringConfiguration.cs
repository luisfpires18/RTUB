using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Trintuna gatherings. The 7 past gatherings are seeded here (HasData: in the migration and in every EnsureCreated
/// test database; SeedData never runs once users exist), undated, without organizer and marked historical.
/// </summary>
public class GatheringConfiguration : IEntityTypeConfiguration<Gathering>
{
    private static readonly DateTime Seeded = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Gathering> builder)
    {
        builder.HasKey(g => g.Id);
        builder.HasIndex(g => g.Number).IsUnique();
        builder.Property(g => g.Place).IsRequired().HasMaxLength(200);
        builder.Property(g => g.Description).HasMaxLength(2000);
        builder.Property(g => g.OrganizerUserId).HasMaxLength(450);
        builder.HasOne(g => g.Organizer)
            .WithMany()
            .HasForeignKey(g => g.OrganizerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasData(
            Seed(1, "Vizela", "Croquetes"),
            Seed(2, "Lamares", "Bronha"),
            Seed(3, "Marco de Canaveses", "Frango"),
            Seed(4, "Chaves", "Conchita"),
            Seed(5, "Fafe", "Coma"),
            Seed(6, "Mogadouro", "Pássaro"),
            Seed(7, "Bragança", "Jeans"));
    }

    private static Gathering Seed(int number, string place, string description) => new()
    {
        Id = number,
        Number = number,
        Place = place,
        Description = description,
        IsHistorical = true,
        CreatedAt = Seeded,
    };
}
