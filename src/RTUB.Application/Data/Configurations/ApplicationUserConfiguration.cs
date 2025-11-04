using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for ApplicationUser entity
/// Configures self-referencing mentor relationship
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Configure self-referencing mentor relationship
        builder.HasOne(u => u.Mentor)
            .WithMany()
            .HasForeignKey(u => u.MentorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
