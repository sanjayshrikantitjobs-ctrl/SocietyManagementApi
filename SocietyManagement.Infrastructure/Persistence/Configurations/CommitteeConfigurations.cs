using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class CommitteeMemberConfiguration : IEntityTypeConfiguration<CommitteeMember>
{
    public void Configure(EntityTypeBuilder<CommitteeMember> builder)
    {
        builder.ToTable("CommitteeMembers");
        builder.HasQueryFilter(c => !c.IsDeleted);
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Designation).HasMaxLength(100).IsRequired();
        builder.Property(c => c.FlatNumber).HasMaxLength(20);
        builder.Property(c => c.Phone).HasMaxLength(20);
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.HasIndex(c => new { c.SocietyId, c.DisplayOrder });

        builder.HasOne(c => c.Society)
            .WithMany()
            .HasForeignKey(c => c.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
