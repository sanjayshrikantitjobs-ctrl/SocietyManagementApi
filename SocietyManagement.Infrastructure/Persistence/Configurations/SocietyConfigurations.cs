using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class SocietyConfiguration : IEntityTypeConfiguration<Society>
{
    public void Configure(EntityTypeBuilder<Society> builder)
    {
        builder.ToTable("Societies");
        builder.HasQueryFilter(s => !s.IsDeleted);
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Address).HasMaxLength(500).IsRequired();
        builder.Property(s => s.City).HasMaxLength(100).IsRequired();
        builder.Property(s => s.State).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Pincode).HasMaxLength(10).IsRequired();
    }
}

public class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.ToTable("Buildings");
        builder.HasQueryFilter(b => !b.IsDeleted);
        builder.Property(b => b.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(b => new { b.SocietyId, b.Name });

        builder.HasOne(b => b.Society)
            .WithMany(s => s.Buildings)
            .HasForeignKey(b => b.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WingConfiguration : IEntityTypeConfiguration<Wing>
{
    public void Configure(EntityTypeBuilder<Wing> builder)
    {
        builder.ToTable("Wings");
        builder.HasQueryFilter(w => !w.IsDeleted);
        builder.Property(w => w.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(w => new { w.BuildingId, w.Name });

        builder.HasOne(w => w.Building)
            .WithMany(b => b.Wings)
            .HasForeignKey(w => w.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
    public void Configure(EntityTypeBuilder<Floor> builder)
    {
        builder.ToTable("Floors");
        builder.HasQueryFilter(f => !f.IsDeleted);
        builder.HasIndex(f => new { f.WingId, f.FloorNumber });

        builder.HasOne(f => f.Wing)
            .WithMany(w => w.Floors)
            .HasForeignKey(f => f.WingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FlatConfiguration : IEntityTypeConfiguration<Flat>
{
    public void Configure(EntityTypeBuilder<Flat> builder)
    {
        builder.ToTable("Flats");
        builder.HasQueryFilter(f => !f.IsDeleted);
        builder.Property(f => f.FlatNumber).HasMaxLength(20).IsRequired();
        builder.Property(f => f.AreaSqFt).HasColumnType("decimal(10,2)");
        builder.Property(f => f.OwnerName).HasMaxLength(150);
        builder.Property(f => f.OwnerPhone).HasMaxLength(20);
        builder.Property(f => f.OwnerEmail).HasMaxLength(256);
        builder.HasIndex(f => new { f.FloorId, f.FlatNumber }).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(f => f.Floor)
            .WithMany(fl => fl.Flats)
            .HasForeignKey(f => f.FloorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ParkingSlotConfiguration : IEntityTypeConfiguration<ParkingSlot>
{
    public void Configure(EntityTypeBuilder<ParkingSlot> builder)
    {
        builder.ToTable("ParkingSlots");
        builder.HasQueryFilter(p => !p.IsDeleted);
        builder.Property(p => p.SlotNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(p => new { p.SocietyId, p.SlotNumber }).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(p => p.Society)
            .WithMany(s => s.ParkingSlots)
            .HasForeignKey(p => p.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.AllocatedFlat)
            .WithMany(f => f.ParkingSlots)
            .HasForeignKey(p => p.AllocatedFlatId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
