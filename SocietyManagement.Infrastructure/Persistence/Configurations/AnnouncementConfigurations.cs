using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("Announcements");
        builder.HasQueryFilter(a => !a.IsDeleted);
        builder.Property(a => a.Title).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(4000).IsRequired();
        builder.Property(a => a.AttachmentUrl).HasMaxLength(500);

        // The two access patterns residents/admins hit constantly: "every
        // published announcement for my society, newest first" and "every
        // Scheduled/Published row" for the lifecycle background sweep.
        builder.HasIndex(a => new { a.SocietyId, a.Status, a.PublishAt });

        builder.HasOne(a => a.Society)
            .WithMany()
            .HasForeignKey(a => a.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AnnouncementReadConfiguration : IEntityTypeConfiguration<AnnouncementRead>
{
    public void Configure(EntityTypeBuilder<AnnouncementRead> builder)
    {
        builder.ToTable("AnnouncementReads");

        // One read-marker per (announcement, user) — the unique index IS the
        // "already read" guard; MarkAnnouncementReadCommand relies on this
        // rather than a race-prone "check then insert".
        builder.HasIndex(r => new { r.AnnouncementId, r.UserId }).IsUnique();

        builder.HasOne(r => r.Announcement)
            .WithMany(a => a.Reads)
            .HasForeignKey(r => r.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
