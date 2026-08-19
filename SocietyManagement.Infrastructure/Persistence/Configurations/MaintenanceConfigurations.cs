using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class MaintenanceCategoryConfiguration : IEntityTypeConfiguration<MaintenanceCategory>
{
    public void Configure(EntityTypeBuilder<MaintenanceCategory> builder)
    {
        builder.ToTable("MaintenanceCategories");
        builder.HasQueryFilter(c => !c.IsDeleted);
        builder.Property(c => c.ChargeName).HasMaxLength(150).IsRequired();
        builder.Property(c => c.MonthlyAmount).HasColumnType("decimal(12,2)");
        builder.HasIndex(c => new { c.SocietyId, c.DisplayOrder });

        builder.HasOne(c => c.Society)
            .WithMany()
            .HasForeignKey(c => c.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaintenanceSettingsConfiguration : IEntityTypeConfiguration<MaintenanceSettings>
{
    public void Configure(EntityTypeBuilder<MaintenanceSettings> builder)
    {
        builder.ToTable("MaintenanceSettings");
        builder.HasQueryFilter(s => !s.IsDeleted);
        builder.Property(s => s.LateFeeAmount).HasColumnType("decimal(12,2)");
        builder.Property(s => s.InvoiceNumberPrefix).HasMaxLength(20).IsRequired();
        builder.Property(s => s.WhatsAppMessageTemplate).HasMaxLength(1000);
        builder.Property(s => s.PdfFooterMessage).HasMaxLength(500);
        builder.HasIndex(s => s.SocietyId).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(s => s.Society)
            .WithMany()
            .HasForeignKey(s => s.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SpecialChargeConfiguration : IEntityTypeConfiguration<SpecialCharge>
{
    public void Configure(EntityTypeBuilder<SpecialCharge> builder)
    {
        builder.ToTable("SpecialCharges");
        builder.HasQueryFilter(c => !c.IsDeleted);
        builder.Property(c => c.ChargeName).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Amount).HasColumnType("decimal(12,2)");
        builder.Property(c => c.Notes).HasMaxLength(500);
        builder.HasIndex(c => c.FlatId);

        builder.HasOne(c => c.Flat)
            .WithMany()
            .HasForeignKey(c => c.FlatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FineRecordConfiguration : IEntityTypeConfiguration<FineRecord>
{
    public void Configure(EntityTypeBuilder<FineRecord> builder)
    {
        builder.ToTable("FineRecords");
        builder.HasQueryFilter(f => !f.IsDeleted);
        builder.Property(f => f.Reason).HasMaxLength(250).IsRequired();
        builder.Property(f => f.Amount).HasColumnType("decimal(12,2)");
        builder.HasIndex(f => new { f.FlatId, f.Status });

        builder.HasOne(f => f.Flat)
            .WithMany()
            .HasForeignKey(f => f.FlatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaintenanceBillConfiguration : IEntityTypeConfiguration<MaintenanceBill>
{
    public void Configure(EntityTypeBuilder<MaintenanceBill> builder)
    {
        builder.ToTable("MaintenanceBills");
        builder.HasQueryFilter(b => !b.IsDeleted);
        builder.Property(b => b.InvoiceNumber).HasMaxLength(30).IsRequired();
        builder.Property(b => b.PreviousBalance).HasColumnType("decimal(12,2)");
        builder.Property(b => b.FineAmount).HasColumnType("decimal(12,2)");
        builder.Property(b => b.TotalAmount).HasColumnType("decimal(12,2)");
        builder.Property(b => b.AmountPaid).HasColumnType("decimal(12,2)");
        builder.Property(b => b.PdfUrl).HasMaxLength(500);
        builder.Property(b => b.OwnerNameSnapshot).HasMaxLength(150);
        builder.Property(b => b.OwnerPhoneSnapshot).HasMaxLength(20);
        builder.HasIndex(b => b.InvoiceNumber).IsUnique();
        builder.HasIndex(b => new { b.FlatId, b.BillMonth }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(b => b.Status);

        builder.HasOne(b => b.Flat)
            .WithMany()
            .HasForeignKey(b => b.FlatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaintenanceBillItemConfiguration : IEntityTypeConfiguration<MaintenanceBillItem>
{
    public void Configure(EntityTypeBuilder<MaintenanceBillItem> builder)
    {
        builder.ToTable("MaintenanceBillItems");
        builder.HasQueryFilter(i => !i.IsDeleted);
        builder.Property(i => i.Description).HasMaxLength(250).IsRequired();
        builder.Property(i => i.Amount).HasColumnType("decimal(12,2)");
        builder.HasIndex(i => i.MaintenanceBillId);

        builder.HasOne(i => i.MaintenanceBill)
            .WithMany(b => b.Items)
            .HasForeignKey(i => i.MaintenanceBillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.MaintenanceCategory)
            .WithMany(c => c.BillItems)
            .HasForeignKey(i => i.MaintenanceCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.SpecialCharge)
            .WithMany(c => c.BillItems)
            .HasForeignKey(i => i.SpecialChargeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.FineRecord)
            .WithMany(f => f.BillItems)
            .HasForeignKey(i => i.FineRecordId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class MaintenancePaymentConfiguration : IEntityTypeConfiguration<MaintenancePayment>
{
    public void Configure(EntityTypeBuilder<MaintenancePayment> builder)
    {
        builder.ToTable("MaintenancePayments");
        builder.HasQueryFilter(p => !p.IsDeleted);
        builder.Property(p => p.Amount).HasColumnType("decimal(12,2)");
        builder.Property(p => p.TransactionReference).HasMaxLength(100);
        builder.Property(p => p.Notes).HasMaxLength(500);
        builder.HasIndex(p => p.MaintenanceBillId);

        builder.HasOne(p => p.MaintenanceBill)
            .WithMany(b => b.Payments)
            .HasForeignKey(p => p.MaintenanceBillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.ReceivedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
