using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> builder)
    {
        builder.ToTable("Complaints");
        builder.HasQueryFilter(c => !c.IsDeleted);
        builder.Property(c => c.RaisedByName).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.ResolutionNotes).HasMaxLength(1000);
        builder.Property(c => c.ReopenReason).HasMaxLength(500);
        builder.HasIndex(c => new { c.SocietyId, c.Status });
        builder.HasIndex(c => c.FlatId);

        builder.HasOne(c => c.Society)
            .WithMany()
            .HasForeignKey(c => c.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Flat)
            .WithMany()
            .HasForeignKey(c => c.FlatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.AssignedStaff)
            .WithMany()
            .HasForeignKey(c => c.AssignedStaffId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
