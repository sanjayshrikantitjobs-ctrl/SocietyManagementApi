using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class WaterTankerLogConfiguration : IEntityTypeConfiguration<WaterTankerLog>
{
    public void Configure(EntityTypeBuilder<WaterTankerLog> builder)
    {
        builder.ToTable("WaterTankerLogs");
        builder.HasQueryFilter(w => !w.IsDeleted);
        builder.Property(w => w.ProviderName).HasMaxLength(150);
        builder.Property(w => w.VehicleNumber).HasMaxLength(20);
        builder.Property(w => w.PricePerTanker).HasColumnType("decimal(12,2)");
        builder.Property(w => w.Notes).HasMaxLength(500);
        builder.HasIndex(w => new { w.SocietyId, w.Date });

        builder.HasOne(w => w.Society)
            .WithMany()
            .HasForeignKey(w => w.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
