using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class WaterTankerCollectionConfiguration : IEntityTypeConfiguration<WaterTankerCollection>
{
    public void Configure(EntityTypeBuilder<WaterTankerCollection> builder)
    {
        builder.ToTable("WaterTankerCollections");
        builder.HasQueryFilter(w => !w.IsDeleted);
        builder.Property(w => w.Amount).HasColumnType("decimal(12,2)");
        builder.Property(w => w.Notes).HasMaxLength(500);
        builder.HasIndex(w => new { w.SocietyId, w.FlatId, w.Month }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(w => new { w.SocietyId, w.Month });

        builder.HasOne(w => w.Society)
            .WithMany()
            .HasForeignKey(w => w.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Flat)
            .WithMany()
            .HasForeignKey(w => w.FlatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
