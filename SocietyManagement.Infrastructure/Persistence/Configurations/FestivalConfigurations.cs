using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence.Configurations;

public class FestivalConfiguration : IEntityTypeConfiguration<Festival>
{
    public void Configure(EntityTypeBuilder<Festival> builder)
    {
        builder.ToTable("Festivals");
        builder.HasQueryFilter(f => !f.IsDeleted);
        builder.Property(f => f.Name).HasMaxLength(150).IsRequired();
        builder.Property(f => f.Description).HasMaxLength(2000);
        builder.Property(f => f.BannerImageUrl).HasMaxLength(500);
        builder.Property(f => f.CoverPhotoUrl).HasMaxLength(500);
        builder.Property(f => f.Theme).HasMaxLength(100);
        builder.HasIndex(f => new { f.SocietyId, f.Year });
        builder.HasIndex(f => f.Status);

        builder.HasOne(f => f.Society)
            .WithMany()
            .HasForeignKey(f => f.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.ParentFestival)
            .WithMany()
            .HasForeignKey(f => f.ParentFestivalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.ContributionPoolFestival)
            .WithMany()
            .HasForeignKey(f => f.ContributionPoolFestivalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FestivalBudgetCategoryConfiguration : IEntityTypeConfiguration<FestivalBudgetCategory>
{
    public void Configure(EntityTypeBuilder<FestivalBudgetCategory> builder)
    {
        builder.ToTable("FestivalBudgetCategories");
        builder.HasQueryFilter(c => !c.IsDeleted);
        builder.Property(c => c.EstimatedAmount).HasColumnType("decimal(12,2)");
        builder.Property(c => c.ApprovedAmount).HasColumnType("decimal(12,2)");
        builder.Property(c => c.Notes).HasMaxLength(500);
        builder.HasIndex(c => new { c.FestivalId, c.Category }).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(c => c.Festival)
            .WithMany(f => f.BudgetCategories)
            .HasForeignKey(c => c.FestivalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FestivalBudgetRevisionConfiguration : IEntityTypeConfiguration<FestivalBudgetRevision>
{
    public void Configure(EntityTypeBuilder<FestivalBudgetRevision> builder)
    {
        builder.ToTable("FestivalBudgetRevisions");
        builder.HasQueryFilter(r => !r.IsDeleted);
        builder.Property(r => r.PreviousEstimatedAmount).HasColumnType("decimal(12,2)");
        builder.Property(r => r.NewEstimatedAmount).HasColumnType("decimal(12,2)");
        builder.Property(r => r.PreviousApprovedAmount).HasColumnType("decimal(12,2)");
        builder.Property(r => r.NewApprovedAmount).HasColumnType("decimal(12,2)");
        builder.Property(r => r.Reason).HasMaxLength(500);

        builder.HasOne(r => r.FestivalBudgetCategory)
            .WithMany(c => c.Revisions)
            .HasForeignKey(r => r.FestivalBudgetCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FestivalContributionConfiguration : IEntityTypeConfiguration<FestivalContribution>
{
    public void Configure(EntityTypeBuilder<FestivalContribution> builder)
    {
        builder.ToTable("FestivalContributions");
        builder.HasQueryFilter(c => !c.IsDeleted);
        builder.Property(c => c.MemberName).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Amount).HasColumnType("decimal(12,2)");
        builder.Property(c => c.TransactionId).HasMaxLength(100);
        builder.Property(c => c.ReceiptNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(c => c.ReceiptNumber).IsUnique();
        builder.HasIndex(c => c.FestivalId);

        builder.HasOne(c => c.Festival)
            .WithMany(f => f.Contributions)
            .HasForeignKey(c => c.FestivalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Flat)
            .WithMany()
            .HasForeignKey(c => c.FlatId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class FestivalFlatTargetConfiguration : IEntityTypeConfiguration<FestivalFlatTarget>
{
    public void Configure(EntityTypeBuilder<FestivalFlatTarget> builder)
    {
        builder.ToTable("FestivalFlatTargets");
        builder.HasQueryFilter(t => !t.IsDeleted);
        builder.Property(t => t.TargetAmount).HasColumnType("decimal(12,2)");
        builder.HasIndex(t => new { t.FestivalId, t.FlatId }).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(t => t.Festival)
            .WithMany(f => f.FlatTargets)
            .HasForeignKey(t => t.FestivalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Flat)
            .WithMany()
            .HasForeignKey(t => t.FlatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FestivalSponsorConfiguration : IEntityTypeConfiguration<FestivalSponsor>
{
    public void Configure(EntityTypeBuilder<FestivalSponsor> builder)
    {
        builder.ToTable("FestivalSponsors");
        builder.HasQueryFilter(s => !s.IsDeleted);
        builder.Property(s => s.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.ContactPerson).HasMaxLength(150);
        builder.Property(s => s.Phone).HasMaxLength(20);
        builder.Property(s => s.Email).HasMaxLength(256);
        builder.Property(s => s.PromisedAmount).HasColumnType("decimal(12,2)");
        builder.Property(s => s.ReceivedAmount).HasColumnType("decimal(12,2)");
        builder.Property(s => s.LogoUrl).HasMaxLength(500);
        builder.Property(s => s.BannerUrl).HasMaxLength(500);
        builder.HasIndex(s => s.FestivalId);

        builder.HasOne(s => s.Festival)
            .WithMany(f => f.Sponsors)
            .HasForeignKey(s => s.FestivalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FestivalVendorConfiguration : IEntityTypeConfiguration<FestivalVendor>
{
    public void Configure(EntityTypeBuilder<FestivalVendor> builder)
    {
        builder.ToTable("FestivalVendors");
        builder.HasQueryFilter(v => !v.IsDeleted);
        builder.Property(v => v.Name).HasMaxLength(200).IsRequired();
        builder.Property(v => v.Phone).HasMaxLength(20);
        builder.Property(v => v.Email).HasMaxLength(256);
        builder.Property(v => v.GstNumber).HasMaxLength(20);
        builder.Property(v => v.Address).HasMaxLength(500);
        builder.Property(v => v.Rating).HasColumnType("decimal(3,2)");
        builder.HasIndex(v => new { v.SocietyId, v.Name });

        builder.HasOne(v => v.Society)
            .WithMany()
            .HasForeignKey(v => v.SocietyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FestivalExpenseConfiguration : IEntityTypeConfiguration<FestivalExpense>
{
    public void Configure(EntityTypeBuilder<FestivalExpense> builder)
    {
        builder.ToTable("FestivalExpenses");
        builder.HasQueryFilter(e => !e.IsDeleted);
        builder.Property(e => e.Amount).HasColumnType("decimal(12,2)");
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.BillImageUrl).HasMaxLength(500);
        builder.Property(e => e.InvoiceNumber).HasMaxLength(100);
        builder.Property(e => e.RejectionReason).HasMaxLength(500);
        builder.HasIndex(e => new { e.FestivalId, e.ApprovalStatus });

        builder.HasOne(e => e.Festival)
            .WithMany(f => f.Expenses)
            .HasForeignKey(e => e.FestivalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.FestivalBudgetCategory)
            .WithMany(c => c.Expenses)
            .HasForeignKey(e => e.FestivalBudgetCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Vendor)
            .WithMany(v => v.Expenses)
            .HasForeignKey(e => e.VendorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.ApprovedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
