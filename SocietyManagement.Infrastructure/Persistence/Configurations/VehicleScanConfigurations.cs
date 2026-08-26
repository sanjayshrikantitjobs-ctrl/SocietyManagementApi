using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class VehicleScanLogConfiguration : IEntityTypeConfiguration<VehicleScanLog>
{
    public void Configure(EntityTypeBuilder<VehicleScanLog> builder)
    {
        builder.ToTable("VehicleScanLogs");
        builder.HasQueryFilter(v => !v.IsDeleted);
        builder.Property(v => v.NormalizedRegistrationNumber).HasMaxLength(20).IsRequired();
        builder.Property(v => v.RawOcrText).HasMaxLength(50);
        builder.Property(v => v.ImageUrl).HasMaxLength(500);
        builder.HasIndex(v => new { v.SocietyId, v.NormalizedRegistrationNumber });
        builder.HasIndex(v => new { v.SocietyId, v.ScannedByUserId });

        builder.HasOne(v => v.Society)
            .WithMany()
            .HasForeignKey(v => v.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Gate)
            .WithMany()
            .HasForeignKey(v => v.GateId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict, not SetNull — Users are soft-deleted, never physically
        // removed, so Restrict never actually blocks a real delete in
        // practice (same reasoning as VisitorVisitConfiguration's user FKs).
        builder.HasOne(v => v.ScannedByUser)
            .WithMany()
            .HasForeignKey(v => v.ScannedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.MatchedVehicle)
            .WithMany()
            .HasForeignKey(v => v.MatchedVehicleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
