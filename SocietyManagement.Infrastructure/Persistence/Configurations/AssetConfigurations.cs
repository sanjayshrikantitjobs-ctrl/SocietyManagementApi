using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(2000);
        builder.Property(a => a.ImageUrl).HasMaxLength(500);

        builder.Property(a => a.RentalPrice).HasColumnType("decimal(12,2)");
        builder.Property(a => a.SecurityDeposit).HasColumnType("decimal(12,2)");
        builder.Property(a => a.DamageCharge).HasColumnType("decimal(12,2)");
        builder.Property(a => a.LateReturnCharge).HasColumnType("decimal(12,2)");

        builder.HasIndex(a => new { a.SocietyId, a.IsActive });

        builder.HasOne(a => a.Society).WithMany().HasForeignKey(a => a.SocietyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AssetBookingConfiguration : IEntityTypeConfiguration<AssetBooking>
{
    public void Configure(EntityTypeBuilder<AssetBooking> builder)
    {
        builder.ToTable("AssetBookings");
        builder.HasQueryFilter(b => !b.IsDeleted);

        builder.Property(b => b.RequestedByName).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Notes).HasMaxLength(2000);
        builder.Property(b => b.RejectionReason).HasMaxLength(500);

        builder.Property(b => b.RentalCharge).HasColumnType("decimal(12,2)");
        builder.Property(b => b.SecurityDepositAmount).HasColumnType("decimal(12,2)");
        builder.Property(b => b.DamageChargeAmount).HasColumnType("decimal(12,2)");
        builder.Property(b => b.LateChargeAmount).HasColumnType("decimal(12,2)");
        builder.Property(b => b.TotalAmount).HasColumnType("decimal(12,2)");
        builder.Property(b => b.DepositRefundAmount).HasColumnType("decimal(12,2)");

        builder.HasIndex(b => new { b.SocietyId, b.Status });
        builder.HasIndex(b => b.FlatId);

        builder.HasOne(b => b.Society).WithMany().HasForeignKey(b => b.SocietyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Flat).WithMany().HasForeignKey(b => b.FlatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.FacilityBooking).WithMany(f => f.LinkedAssetBookings)
            .HasForeignKey(b => b.FacilityBookingId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class AssetBookingItemConfiguration : IEntityTypeConfiguration<AssetBookingItem>
{
    public void Configure(EntityTypeBuilder<AssetBookingItem> builder)
    {
        builder.ToTable("AssetBookingItems");

        builder.Property(i => i.UnitPrice).HasColumnType("decimal(12,2)");
        builder.Property(i => i.LineTotal).HasColumnType("decimal(12,2)");

        // Backs the quantity-availability check the booking handler runs
        // inside a Serializable transaction — see AssetBookingFeature's
        // CreateAssetBookingCommandHandler.
        builder.HasIndex(i => i.AssetId);

        builder.HasOne(i => i.AssetBooking).WithMany(b => b.Items).HasForeignKey(i => i.AssetBookingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Asset).WithMany(a => a.BookingItems).HasForeignKey(i => i.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}
