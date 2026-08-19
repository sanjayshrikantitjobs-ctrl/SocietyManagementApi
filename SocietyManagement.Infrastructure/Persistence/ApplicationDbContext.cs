using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Society> Societies => Set<Society>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Wing> Wings => Set<Wing>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Flat> Flats => Set<Flat>();
    public DbSet<ParkingSlot> ParkingSlots => Set<ParkingSlot>();

    public DbSet<Festival> Festivals => Set<Festival>();
    public DbSet<FestivalBudgetCategory> FestivalBudgetCategories => Set<FestivalBudgetCategory>();
    public DbSet<FestivalBudgetRevision> FestivalBudgetRevisions => Set<FestivalBudgetRevision>();
    public DbSet<FestivalContribution> FestivalContributions => Set<FestivalContribution>();
    public DbSet<FestivalSponsor> FestivalSponsors => Set<FestivalSponsor>();
    public DbSet<FestivalVendor> FestivalVendors => Set<FestivalVendor>();
    public DbSet<FestivalExpense> FestivalExpenses => Set<FestivalExpense>();

    public DbSet<MaintenanceCategory> MaintenanceCategories => Set<MaintenanceCategory>();
    public DbSet<MaintenanceSettings> MaintenanceSettings => Set<MaintenanceSettings>();
    public DbSet<SpecialCharge> SpecialCharges => Set<SpecialCharge>();
    public DbSet<FineRecord> FineRecords => Set<FineRecord>();
    public DbSet<MaintenanceBill> MaintenanceBills => Set<MaintenanceBill>();
    public DbSet<MaintenanceBillItem> MaintenanceBillItems => Set<MaintenanceBillItem>();
    public DbSet<MaintenancePayment> MaintenancePayments => Set<MaintenancePayment>();

    public DbSet<Member> Members => Set<Member>();
    public DbSet<FlatResidency> FlatResidencies => Set<FlatResidency>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<FlatResaleListing> FlatResaleListings => Set<FlatResaleListing>();

    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventRsvp> EventRsvps => Set<EventRsvp>();

    public DbSet<Gate> Gates => Set<Gate>();
    public DbSet<VisitorPurpose> VisitorPurposes => Set<VisitorPurpose>();
    public DbSet<Visitor> Visitors => Set<Visitor>();
    public DbSet<VisitorVisit> VisitorVisits => Set<VisitorVisit>();
    public DbSet<VisitorSettings> VisitorSettings => Set<VisitorSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // BaseEntity.DomainEvents is an in-memory buffer (see
        // DispatchDomainEventsInterceptor), not a persisted relationship —
        // without this, EF's convention-based discovery tries to map
        // BaseEvent as an entity and fails design-time model creation with
        // "'BaseEvent' requires a primary key to be defined."
        builder.Ignore<Domain.Common.BaseEvent>();

        // Applies every IEntityTypeConfiguration<T> in this assembly
        // (Persistence/Configurations/*) — new modules just add a class there.
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(builder);
    }
}
