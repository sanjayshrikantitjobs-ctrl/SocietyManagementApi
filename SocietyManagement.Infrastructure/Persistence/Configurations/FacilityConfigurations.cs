using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> builder)
    {
        builder.ToTable("Facilities");
        builder.HasQueryFilter(f => !f.IsDeleted);

        builder.Property(f => f.Name).HasMaxLength(200).IsRequired();
        builder.Property(f => f.Description).HasMaxLength(2000);
        builder.Property(f => f.ImageUrl).HasMaxLength(500);
        builder.Property(f => f.Location).HasMaxLength(300);

        builder.Property(f => f.PricePerUnit).HasColumnType("decimal(12,2)");
        builder.Property(f => f.SecurityDeposit).HasColumnType("decimal(12,2)");
        builder.Property(f => f.CleaningCharge).HasColumnType("decimal(12,2)");
        builder.Property(f => f.AdditionalCharge).HasColumnType("decimal(12,2)");

        builder.HasIndex(f => new { f.SocietyId, f.IsActive });

        builder.HasOne(f => f.Society).WithMany().HasForeignKey(f => f.SocietyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FacilityBlackoutDateConfiguration : IEntityTypeConfiguration<FacilityBlackoutDate>
{
    public void Configure(EntityTypeBuilder<FacilityBlackoutDate> builder)
    {
        builder.ToTable("FacilityBlackoutDates");
        builder.Property(b => b.Reason).HasMaxLength(300);

        builder.HasIndex(b => new { b.FacilityId, b.BlackoutDate }).IsUnique();

        builder.HasOne(b => b.Facility).WithMany(f => f.BlackoutDates).HasForeignKey(b => b.FacilityId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FacilityBookingConfiguration : IEntityTypeConfiguration<FacilityBooking>
{
    public void Configure(EntityTypeBuilder<FacilityBooking> builder)
    {
        builder.ToTable("FacilityBookings");
        builder.HasQueryFilter(b => !b.IsDeleted);

        builder.Property(b => b.BookedByName).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Purpose).HasMaxLength(300);
        builder.Property(b => b.Notes).HasMaxLength(2000);
        builder.Property(b => b.RejectionReason).HasMaxLength(500);

        builder.Property(b => b.RentalCharge).HasColumnType("decimal(12,2)");
        builder.Property(b => b.SecurityDepositAmount).HasColumnType("decimal(12,2)");
        builder.Property(b => b.CleaningChargeAmount).HasColumnType("decimal(12,2)");
        builder.Property(b => b.AdditionalChargeAmount).HasColumnType("decimal(12,2)");
        builder.Property(b => b.TotalAmount).HasColumnType("decimal(12,2)");
        builder.Property(b => b.DepositRefundAmount).HasColumnType("decimal(12,2)");

        // Backs the overlap check the booking handler runs inside a
        // Serializable transaction to block concurrent double-booking —
        // see FacilityBookingFeature's CreateFacilityBookingCommandHandler.
        builder.HasIndex(b => new { b.FacilityId, b.BookingDate, b.Status });
        builder.HasIndex(b => new { b.SocietyId, b.Status });
        builder.HasIndex(b => b.FlatId);

        builder.HasOne(b => b.Society).WithMany().HasForeignKey(b => b.SocietyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Facility).WithMany(f => f.Bookings).HasForeignKey(b => b.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Flat).WithMany().HasForeignKey(b => b.FlatId).OnDelete(DeleteBehavior.Restrict);
    }
}
