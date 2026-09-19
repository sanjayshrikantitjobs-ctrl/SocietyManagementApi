using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");
        builder.HasQueryFilter(i => !i.IsDeleted);
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Category).HasMaxLength(100);
        builder.Property(i => i.Unit).HasMaxLength(30).IsRequired();
        builder.Property(i => i.MinimumStock).HasColumnType("decimal(12,2)");
        builder.HasIndex(i => new { i.SocietyId, i.IsActive });
        builder.HasOne(i => i.Society).WithMany().HasForeignKey(i => i.SocietyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> builder)
    {
        builder.ToTable("StockTransactions");
        builder.HasQueryFilter(t => !t.IsDeleted);
        builder.Property(t => t.Quantity).HasColumnType("decimal(12,2)");
        builder.Property(t => t.IssuedTo).HasMaxLength(150);
        builder.Property(t => t.LocationOfUse).HasMaxLength(150);
        builder.Property(t => t.Notes).HasMaxLength(500);
        builder.HasIndex(t => new { t.InventoryItemId, t.TransactionDate });
        builder.HasOne(t => t.Society).WithMany().HasForeignKey(t => t.SocietyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.InventoryItem).WithMany().HasForeignKey(t => t.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
{
    public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
    {
        builder.ToTable("PurchaseRequests");
        builder.HasQueryFilter(p => !p.IsDeleted);
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.RequestedByName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.RejectionReason).HasMaxLength(500);
        builder.HasIndex(p => new { p.SocietyId, p.Status });
        builder.HasOne(p => p.Society).WithMany().HasForeignKey(p => p.SocietyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Vendor).WithMany().HasForeignKey(p => p.VendorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseRequestItemConfiguration : IEntityTypeConfiguration<PurchaseRequestItem>
{
    public void Configure(EntityTypeBuilder<PurchaseRequestItem> builder)
    {
        builder.ToTable("PurchaseRequestItems");
        builder.Property(i => i.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Unit).HasMaxLength(30).IsRequired();
        builder.Property(i => i.Quantity).HasColumnType("decimal(12,2)");
        builder.Property(i => i.ReceivedQuantity).HasColumnType("decimal(12,2)");
        builder.Property(i => i.EstimatedUnitPrice).HasColumnType("decimal(12,2)");
        builder.HasOne(i => i.PurchaseRequest).WithMany(p => p.Items).HasForeignKey(i => i.PurchaseRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.InventoryItem).WithMany().HasForeignKey(i => i.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets");
        builder.HasQueryFilter(b => !b.IsDeleted);
        builder.Property(b => b.Amount).HasColumnType("decimal(14,2)");
        builder.HasIndex(b => new { b.SocietyId, b.FinancialYear, b.Category }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(b => b.Society).WithMany().HasForeignKey(b => b.SocietyId).OnDelete(DeleteBehavior.Restrict);
    }
}
