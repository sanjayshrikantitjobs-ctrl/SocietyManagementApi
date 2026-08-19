using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events");
        builder.HasQueryFilter(e => !e.IsDeleted);
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Venue).HasMaxLength(200);
        builder.HasIndex(e => new { e.SocietyId, e.Status });

        builder.HasOne(e => e.Society)
            .WithMany()
            .HasForeignKey(e => e.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Festival)
            .WithMany()
            .HasForeignKey(e => e.FestivalId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class EventRsvpConfiguration : IEntityTypeConfiguration<EventRsvp>
{
    public void Configure(EntityTypeBuilder<EventRsvp> builder)
    {
        builder.ToTable("EventRsvps");
        builder.HasQueryFilter(r => !r.IsDeleted);
        builder.Property(r => r.QrToken).HasMaxLength(64).IsRequired();
        builder.HasIndex(r => r.QrToken).IsUnique();
        builder.HasIndex(r => new { r.EventId, r.FlatId }).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(r => r.Event)
            .WithMany(e => e.Rsvps)
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Flat)
            .WithMany()
            .HasForeignKey(r => r.FlatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Member)
            .WithMany()
            .HasForeignKey(r => r.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.CheckedInByUser)
            .WithMany()
            .HasForeignKey(r => r.CheckedInByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
