using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("Staff");
        builder.HasQueryFilter(s => !s.IsDeleted);
        builder.Property(s => s.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.LastName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Phone).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Email).HasMaxLength(256);
        builder.Property(s => s.Address).HasMaxLength(500);
        builder.Property(s => s.Salary).HasColumnType("decimal(10,2)");
        builder.HasIndex(s => new { s.SocietyId, s.Category });

        builder.HasOne(s => s.Society)
            .WithMany()
            .HasForeignKey(s => s.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SocietyServiceConfiguration : IEntityTypeConfiguration<SocietyService>
{
    public void Configure(EntityTypeBuilder<SocietyService> builder)
    {
        builder.ToTable("SocietyServices");
        builder.HasQueryFilter(s => !s.IsDeleted);
        builder.Property(s => s.ServiceName).HasMaxLength(150).IsRequired();
        builder.Property(s => s.VendorName).HasMaxLength(150).IsRequired();
        builder.Property(s => s.ContactPerson).HasMaxLength(150);
        builder.Property(s => s.ContactNumber).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Email).HasMaxLength(256);
        builder.HasIndex(s => new { s.SocietyId, s.RenewalDate });

        builder.HasOne(s => s.Society)
            .WithMany()
            .HasForeignKey(s => s.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
