using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("People");
        builder.HasQueryFilter(p => !p.IsDeleted);
        builder.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Phone).HasMaxLength(20);
        builder.Property(p => p.Email).HasMaxLength(256);
        builder.Property(p => p.WhatsAppNumber).HasMaxLength(20);
        builder.Property(p => p.PhotoUrl).HasMaxLength(500);
        builder.Property(p => p.AadhaarNumber).HasMaxLength(20);
        builder.Property(p => p.PanNumber).HasMaxLength(10);
        builder.HasIndex(p => new { p.SocietyId, p.Phone });

        builder.HasOne(p => p.Society)
            .WithMany()
            .HasForeignKey(p => p.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FlatOccupancyConfiguration : IEntityTypeConfiguration<FlatOccupancy>
{
    public void Configure(EntityTypeBuilder<FlatOccupancy> builder)
    {
        builder.ToTable("FlatOccupancies");
        builder.HasQueryFilter(o => !o.IsDeleted);
        builder.Property(o => o.Notes).HasMaxLength(500);
        builder.HasIndex(o => new { o.FlatId, o.Type, o.EndDate });

        builder.HasOne(o => o.Flat)
            .WithMany()
            .HasForeignKey(o => o.FlatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.RentalAgreement)
            .WithOne(r => r.FlatOccupancy)
            .HasForeignKey<RentalAgreement>(r => r.FlatOccupancyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OccupancyMemberConfiguration : IEntityTypeConfiguration<OccupancyMember>
{
    public void Configure(EntityTypeBuilder<OccupancyMember> builder)
    {
        builder.ToTable("OccupancyMembers");
        builder.HasQueryFilter(m => !m.IsDeleted);
        builder.HasIndex(m => new { m.FlatOccupancyId, m.LeftDate });
        builder.HasIndex(m => m.PersonId);

        builder.HasOne(m => m.FlatOccupancy)
            .WithMany(o => o.Members)
            .HasForeignKey(m => m.FlatOccupancyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Person)
            .WithMany(p => p.OccupancyMemberships)
            .HasForeignKey(m => m.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RentalAgreementConfiguration : IEntityTypeConfiguration<RentalAgreement>
{
    public void Configure(EntityTypeBuilder<RentalAgreement> builder)
    {
        builder.ToTable("RentalAgreements");
        builder.HasQueryFilter(r => !r.IsDeleted);
        builder.Property(r => r.SecurityDeposit).HasColumnType("decimal(14,2)");
        builder.Property(r => r.RentAmount).HasColumnType("decimal(14,2)");
        builder.Property(r => r.PoliceVerificationReference).HasMaxLength(100);
        builder.Property(r => r.AgreementDocumentUrl).HasMaxLength(500);
        builder.HasIndex(r => r.FlatOccupancyId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ResidentDocumentConfiguration : IEntityTypeConfiguration<ResidentDocument>
{
    public void Configure(EntityTypeBuilder<ResidentDocument> builder)
    {
        builder.ToTable("ResidentDocuments");
        builder.HasQueryFilter(d => !d.IsDeleted);
        builder.Property(d => d.DocumentUrl).HasMaxLength(500).IsRequired();
        builder.Property(d => d.Notes).HasMaxLength(500);
        // Not unique — unlimited documents per occupancy, including
        // multiple "Other" uploads (unlike RentalAgreement's 1:1 relation).
        builder.HasIndex(d => d.FlatOccupancyId);

        // Restrict, not SetNull — Users are soft-deleted, never physically
        // removed, so Restrict never actually blocks a real delete in
        // practice (same reasoning as VehicleScanLogConfiguration's user FK).
        builder.HasOne(d => d.UploadedByUser)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OccupancySettingsConfiguration : IEntityTypeConfiguration<OccupancySettings>
{
    public void Configure(EntityTypeBuilder<OccupancySettings> builder)
    {
        builder.ToTable("OccupancySettings");
        builder.HasQueryFilter(s => !s.IsDeleted);
        builder.HasIndex(s => s.SocietyId).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(s => s.Society)
            .WithMany()
            .HasForeignKey(s => s.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
