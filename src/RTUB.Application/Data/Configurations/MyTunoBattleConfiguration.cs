using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class MyTunoBattleConfiguration : IEntityTypeConfiguration<MyTunoBattle>
{
    public void Configure(EntityTypeBuilder<MyTunoBattle> builder)
    {
        builder.Property(b => b.ReplayJson)
            .HasColumnType("nvarchar(max)");

        builder.HasOne(b => b.AttackerCharacter)
            .WithMany()
            .HasForeignKey(b => b.AttackerCharacterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.DefenderCharacter)
            .WithMany()
            .HasForeignKey(b => b.DefenderCharacterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.AttackerCharacterId, b.StartedAt });
        builder.HasIndex(b => new { b.DefenderCharacterId, b.StartedAt });
    }
}
