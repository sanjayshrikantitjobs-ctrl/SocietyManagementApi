using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> builder)
    {
        builder.ToTable("Pets");
        builder.HasQueryFilter(p => !p.IsDeleted);
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Breed).HasMaxLength(100);
        builder.Property(p => p.PhotoUrl).HasMaxLength(500);
        builder.Property(p => p.RegistrationNumber).HasMaxLength(100);
        builder.Property(p => p.Identification).HasMaxLength(300);
        builder.Property(p => p.Notes).HasMaxLength(1000);
        builder.HasIndex(p => new { p.SocietyId, p.FlatId });
        builder.HasOne(p => p.Society).WithMany().HasForeignKey(p => p.SocietyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Flat).WithMany().HasForeignKey(p => p.FlatId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StaffAttendanceConfiguration : IEntityTypeConfiguration<StaffAttendance>
{
    public void Configure(EntityTypeBuilder<StaffAttendance> builder)
    {
        builder.ToTable("StaffAttendances");
        builder.HasQueryFilter(a => !a.IsDeleted);
        builder.Property(a => a.Date).HasColumnType("date");
        builder.Property(a => a.Notes).HasMaxLength(500);
        builder.HasIndex(a => new { a.StaffId, a.Date }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(a => new { a.SocietyId, a.Date });
        builder.HasOne(a => a.Society).WithMany().HasForeignKey(a => a.SocietyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Staff).WithMany().HasForeignKey(a => a.StaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SocietyDocumentConfiguration : IEntityTypeConfiguration<SocietyDocument>
{
    public void Configure(EntityTypeBuilder<SocietyDocument> builder)
    {
        builder.ToTable("SocietyDocuments");
        builder.HasQueryFilter(d => !d.IsDeleted);
        builder.Property(d => d.Title).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(1000);
        builder.Property(d => d.FileUrl).HasMaxLength(500).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(255);
        builder.HasIndex(d => new { d.SocietyId, d.Category });
        builder.HasOne(d => d.Society).WithMany().HasForeignKey(d => d.SocietyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("Vendors");
        builder.HasQueryFilter(v => !v.IsDeleted);
        builder.Property(v => v.Name).HasMaxLength(200).IsRequired();
        builder.Property(v => v.ContactPerson).HasMaxLength(150);
        builder.Property(v => v.Phone).HasMaxLength(20).IsRequired();
        builder.Property(v => v.Email).HasMaxLength(256);
        builder.Property(v => v.Address).HasMaxLength(500);
        builder.Property(v => v.GstNumber).HasMaxLength(20);
        builder.Property(v => v.PerformanceNotes).HasMaxLength(2000);
        builder.HasIndex(v => new { v.SocietyId, v.Category });
        builder.HasOne(v => v.Society).WithMany().HasForeignKey(v => v.SocietyId).OnDelete(DeleteBehavior.Restrict);
    }
}
