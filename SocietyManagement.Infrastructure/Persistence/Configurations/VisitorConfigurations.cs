using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class GateConfiguration : IEntityTypeConfiguration<Gate>
{
    public void Configure(EntityTypeBuilder<Gate> builder)
    {
        builder.ToTable("Gates");
        builder.HasQueryFilter(g => !g.IsDeleted);
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();
        builder.Property(g => g.Code).HasMaxLength(30).IsRequired();
        builder.Property(g => g.Location).HasMaxLength(200);
        builder.HasIndex(g => new { g.SocietyId, g.Code }).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(g => g.Society)
            .WithMany()
            .HasForeignKey(g => g.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VisitorPurposeConfiguration : IEntityTypeConfiguration<VisitorPurpose>
{
    public void Configure(EntityTypeBuilder<VisitorPurpose> builder)
    {
        builder.ToTable("VisitorPurposes");
        builder.HasQueryFilter(p => !p.IsDeleted);
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(p => new { p.SocietyId, p.Name }).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(p => p.Society)
            .WithMany()
            .HasForeignKey(p => p.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VisitorConfiguration : IEntityTypeConfiguration<Visitor>
{
    public void Configure(EntityTypeBuilder<Visitor> builder)
    {
        builder.ToTable("Visitors");
        builder.HasQueryFilter(v => !v.IsDeleted);
        builder.Property(v => v.Name).HasMaxLength(150).IsRequired();
        builder.Property(v => v.MobileNumber).HasMaxLength(20).IsRequired();
        builder.Property(v => v.PhotoUrl).HasMaxLength(500);
        builder.Property(v => v.VehicleNumber).HasMaxLength(20);
        builder.Property(v => v.VehicleType).HasMaxLength(50);
        builder.Property(v => v.IdType).HasMaxLength(50);
        builder.Property(v => v.IdReference).HasMaxLength(100);
        builder.Property(v => v.Notes).HasMaxLength(500);
        builder.HasIndex(v => new { v.SocietyId, v.MobileNumber });
        builder.HasIndex(v => v.VehicleNumber);

        builder.HasOne(v => v.Society)
            .WithMany()
            .HasForeignKey(v => v.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VisitorVisitConfiguration : IEntityTypeConfiguration<VisitorVisit>
{
    public void Configure(EntityTypeBuilder<VisitorVisit> builder)
    {
        builder.ToTable("VisitorVisits");
        builder.HasQueryFilter(v => !v.IsDeleted);
        builder.Property(v => v.RejectionReason).HasMaxLength(500);
        builder.Property(v => v.ApprovalToken).HasMaxLength(64);
        builder.HasIndex(v => new { v.SocietyId, v.Status });
        builder.HasIndex(v => new { v.FlatId, v.Status });
        builder.HasIndex(v => v.GateId);
        builder.HasIndex(v => v.RequestedAt);
        builder.HasIndex(v => v.ApprovalToken).HasFilter("[ApprovalToken] IS NOT NULL");

        builder.HasOne(v => v.Society)
            .WithMany()
            .HasForeignKey(v => v.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Visitor)
            .WithMany(vis => vis.Visits)
            .HasForeignKey(v => v.VisitorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Flat)
            .WithMany()
            .HasForeignKey(v => v.FlatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Purpose)
            .WithMany()
            .HasForeignKey(v => v.PurposeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Gate)
            .WithMany()
            .HasForeignKey(v => v.GateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.CreatedByUser)
            .WithMany()
            .HasForeignKey(v => v.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull here (as used for single nullable-user FKs elsewhere, e.g.
        // FestivalExpense.ApprovedByUserId) triggers SQL Server's "multiple
        // cascade paths" error once a table has several nullable FKs to the
        // same target — Users is already Restrict via CreatedByUserId above,
        // so these follow the same NO ACTION behavior. Users are soft-deleted
        // (IsDeleted flag) rather than physically removed, so Restrict never
        // actually blocks a real delete in practice.
        builder.HasOne(v => v.ApprovedByUser)
            .WithMany()
            .HasForeignKey(v => v.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.RejectedByUser)
            .WithMany()
            .HasForeignKey(v => v.RejectedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.CheckedInByUser)
            .WithMany()
            .HasForeignKey(v => v.CheckedInByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.CheckedOutByUser)
            .WithMany()
            .HasForeignKey(v => v.CheckedOutByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VisitorSettingsConfiguration : IEntityTypeConfiguration<VisitorSettings>
{
    public void Configure(EntityTypeBuilder<VisitorSettings> builder)
    {
        builder.ToTable("VisitorSettings");
        builder.HasQueryFilter(s => !s.IsDeleted);
        builder.HasIndex(s => s.SocietyId).IsUnique();

        builder.HasOne(s => s.Society)
            .WithMany()
            .HasForeignKey(s => s.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
