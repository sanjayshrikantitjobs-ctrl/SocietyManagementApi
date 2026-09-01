using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class ParkingFineConfiguration : IEntityTypeConfiguration<ParkingFine>
{
    public void Configure(EntityTypeBuilder<ParkingFine> builder)
    {
        builder.ToTable("ParkingFines");
        builder.HasQueryFilter(f => !f.IsDeleted);
        builder.Property(f => f.Notes).HasMaxLength(500);
        builder.Property(f => f.PhotoUrl).HasMaxLength(500);
        builder.Property(f => f.Amount).HasColumnType("decimal(12,2)");
        builder.HasIndex(f => new { f.SocietyId, f.FineDate });
        builder.HasIndex(f => f.VehicleId);

        builder.HasOne(f => f.Society)
            .WithMany()
            .HasForeignKey(f => f.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Vehicle)
            .WithMany()
            .HasForeignKey(f => f.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.ParkingSlot)
            .WithMany()
            .HasForeignKey(f => f.ParkingSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict, not SetNull — Users are soft-deleted, never physically
        // removed, so Restrict never actually blocks a real delete in
        // practice (same reasoning as VehicleScanLogConfiguration's user FK).
        builder.HasOne(f => f.IssuedByUser)
            .WithMany()
            .HasForeignKey(f => f.IssuedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
