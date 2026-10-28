using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class LoginEventConfiguration : IEntityTypeConfiguration<LoginEvent>
{
    public void Configure(EntityTypeBuilder<LoginEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .HasMaxLength(450)
            .IsRequired();

        // No foreign key on purpose: the history outlives a deleted account and the raw-SQL writers stay simple.
        // The dashboard reads by time; the "seen this hour" check reads by user then time.
        builder.HasIndex(e => e.At);
        builder.HasIndex(e => new { e.UserId, e.At });
    }
}
