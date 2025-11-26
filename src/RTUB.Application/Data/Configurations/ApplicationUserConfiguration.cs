using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for ApplicationUser entity
/// Configures self-referencing mentor relationship, PhoneNumber nullability,
/// and primitive collections for Positions and Categories
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

        // Explicitly configure PhoneNumber as nullable to override the [Required] attribute
        // The [Required] attribute is for model validation, not database constraints
        builder.Property(u => u.PhoneNumber)
            .IsRequired(false);

        // Configure Positions as a primitive collection stored in a single column
        // EF Core 10 handles the conversion to/from comma-separated integers
        builder.PrimitiveCollection(u => u.Positions)
            .ElementType()
            .HasConversion<int>();

        // Configure Categories as a primitive collection stored in a single column
        // EF Core 10 handles the conversion to/from comma-separated integers
        builder.PrimitiveCollection(u => u.Categories)
            .ElementType()
            .HasConversion<int>();
    }
}
