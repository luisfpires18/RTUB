using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class MyTunoChallengeRequestConfiguration : IEntityTypeConfiguration<MyTunoChallengeRequest>
{
    public void Configure(EntityTypeBuilder<MyTunoChallengeRequest> builder)
    {
        builder.Property(r => r.RequesterUserId)
            .IsRequired();

        builder.Property(r => r.TargetUserId)
            .IsRequired();

        builder.HasOne(r => r.Requester)
            .WithMany()
            .HasForeignKey(r => r.RequesterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Target)
            .WithMany()
            .HasForeignKey(r => r.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.TargetUserId, r.Status });
    }
}
