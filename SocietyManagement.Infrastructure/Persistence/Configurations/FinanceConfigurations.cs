using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");
        builder.HasQueryFilter(e => !e.IsDeleted);
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Amount).HasColumnType("decimal(10,2)");
        builder.Property(e => e.PaidTo).HasMaxLength(150);
        builder.HasIndex(e => new { e.SocietyId, e.ExpenseDate });

        builder.HasOne(e => e.Society)
            .WithMany()
            .HasForeignKey(e => e.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Staff)
            .WithMany()
            .HasForeignKey(e => e.StaffId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
