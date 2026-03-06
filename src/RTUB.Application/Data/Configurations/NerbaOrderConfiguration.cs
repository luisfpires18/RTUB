using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for NerbaOrder entity
/// Configures relationships and indexes for optimal query performance
/// </summary>
public class NerbaOrderConfiguration : IEntityTypeConfiguration<NerbaOrder>
{
    public void Configure(EntityTypeBuilder<NerbaOrder> builder)
    {
        // Configure table name
        builder.ToTable("NerbaOrders");

        // Configure foreign key relationship to Report
        builder.HasOne(no => no.Report)
            .WithMany()
            .HasForeignKey(no => no.ReportId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index for filtering orders by report
        builder.HasIndex(no => no.ReportId)
            .HasDatabaseName("IX_NerbaOrders_ReportId");

        // Configure foreign key relationship to Event (required - all orders belong to a NERBA event)
        builder.HasOne(no => no.Event)
            .WithMany()
            .HasForeignKey(no => no.EventId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Index for filtering orders by event
        builder.HasIndex(no => no.EventId)
            .HasDatabaseName("IX_NerbaOrders_EventId");
    }
}
