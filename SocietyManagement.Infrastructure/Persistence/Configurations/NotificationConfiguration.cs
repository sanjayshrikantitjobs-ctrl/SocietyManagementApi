using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.Property(n => n.EventType).HasMaxLength(100).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.DedupeKey).HasMaxLength(200);

        // The one access pattern this table exists for: "my notifications,
        // newest first" and its unread-count sibling.
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        builder.HasIndex(n => new { n.UserId, n.IsRead });

        // SQL Server unique indexes treat each NULL as distinct from every
        // other NULL, so rows with no DedupeKey never collide with each
        // other — dedupe is only enforced when a call site supplies one.
        builder.HasIndex(n => new { n.UserId, n.EventType, n.DedupeKey }).IsUnique();

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
