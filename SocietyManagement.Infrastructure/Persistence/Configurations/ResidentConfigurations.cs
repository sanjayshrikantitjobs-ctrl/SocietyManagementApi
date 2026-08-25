using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members");
        builder.HasQueryFilter(m => !m.IsDeleted);
        builder.Property(m => m.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(m => m.LastName).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Phone).HasMaxLength(20).IsRequired();
        builder.Property(m => m.Email).HasMaxLength(256);
        builder.Property(m => m.PhotoUrl).HasMaxLength(500);
        builder.HasIndex(m => new { m.SocietyId, m.Phone });

        builder.HasOne(m => m.Society)
            .WithMany()
            .HasForeignKey(m => m.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);

        // The inverse (User.Member) FK is configured on UserConfiguration
        // since Users.MemberId is the physical column.
    }
}

public class FlatResidencyConfiguration : IEntityTypeConfiguration<FlatResidency>
{
    public void Configure(EntityTypeBuilder<FlatResidency> builder)
    {
        builder.ToTable("FlatResidencies");
        builder.HasQueryFilter(r => !r.IsDeleted);
        builder.Property(r => r.Notes).HasMaxLength(500);
        builder.HasIndex(r => new { r.FlatId, r.MoveOutDate });
        builder.HasIndex(r => r.MemberId);

        builder.HasOne(r => r.Flat)
            .WithMany(f => f.Residencies)
            .HasForeignKey(r => r.FlatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Member)
            .WithMany(m => m.Residencies)
            .HasForeignKey(r => r.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasQueryFilter(v => !v.IsDeleted);
        builder.Property(v => v.RegistrationNumber).HasMaxLength(20).IsRequired();
        builder.Property(v => v.Make).HasMaxLength(100);
        builder.Property(v => v.Model).HasMaxLength(100);
        builder.Property(v => v.Color).HasMaxLength(50);
        builder.HasIndex(v => v.MemberId);
        builder.HasIndex(v => v.FlatId);
        builder.HasIndex(v => v.RegistrationNumber).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(v => v.Member)
            .WithMany(m => m.Vehicles)
            .HasForeignKey(v => v.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Flat)
            .WithMany()
            .HasForeignKey(v => v.FlatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.ParkingSlot)
            .WithMany()
            .HasForeignKey(v => v.ParkingSlotId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class EmergencyContactConfiguration : IEntityTypeConfiguration<EmergencyContact>
{
    public void Configure(EntityTypeBuilder<EmergencyContact> builder)
    {
        builder.ToTable("EmergencyContacts");
        builder.HasQueryFilter(c => !c.IsDeleted);
        builder.Property(c => c.ContactName).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Relationship).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(20).IsRequired();
        builder.Property(c => c.AlternatePhone).HasMaxLength(20);
        builder.HasIndex(c => c.FlatId);

        builder.HasOne(c => c.Flat)
            .WithMany()
            .HasForeignKey(c => c.FlatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FlatResaleListingConfiguration : IEntityTypeConfiguration<FlatResaleListing>
{
    public void Configure(EntityTypeBuilder<FlatResaleListing> builder)
    {
        builder.ToTable("FlatResaleListings");
        builder.HasQueryFilter(l => !l.IsDeleted);
        builder.Property(l => l.AskingPrice).HasColumnType("decimal(14,2)");
        builder.Property(l => l.VisitTimingNotes).HasMaxLength(250);
        builder.Property(l => l.NocDocumentUrl).HasMaxLength(500);
        builder.Property(l => l.Notes).HasMaxLength(500);
        builder.HasIndex(l => new { l.FlatId, l.Status });

        builder.HasOne(l => l.Flat)
            .WithMany()
            .HasForeignKey(l => l.FlatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.ListedByMember)
            .WithMany()
            .HasForeignKey(l => l.ListedByMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
