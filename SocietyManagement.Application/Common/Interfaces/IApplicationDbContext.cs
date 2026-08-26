using Microsoft.EntityFrameworkCore;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>
/// The Application layer depends on this abstraction, not on
/// Microsoft.EntityFrameworkCore.DbContext directly, so Application has zero
/// reference to Infrastructure/EF Core (Clean Architecture dependency rule).
/// Implemented by Infrastructure.Persistence.ApplicationDbContext.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<OtpVerification> OtpVerifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    DbSet<Society> Societies { get; }
    DbSet<Building> Buildings { get; }
    DbSet<Wing> Wings { get; }
    DbSet<Floor> Floors { get; }
    DbSet<Flat> Flats { get; }
    DbSet<ParkingSlot> ParkingSlots { get; }

    DbSet<Festival> Festivals { get; }
    DbSet<FestivalBudgetCategory> FestivalBudgetCategories { get; }
    DbSet<FestivalBudgetRevision> FestivalBudgetRevisions { get; }
    DbSet<FestivalContribution> FestivalContributions { get; }
    DbSet<FestivalSponsor> FestivalSponsors { get; }
    DbSet<FestivalVendor> FestivalVendors { get; }
    DbSet<FestivalVolunteer> FestivalVolunteers { get; }
    DbSet<FestivalTask> FestivalTasks { get; }
    DbSet<FestivalExpense> FestivalExpenses { get; }
    DbSet<FestivalFlatTarget> FestivalFlatTargets { get; }
    DbSet<WaterTankerCollection> WaterTankerCollections { get; }

    DbSet<MaintenanceCategory> MaintenanceCategories { get; }
    DbSet<MaintenanceSettings> MaintenanceSettings { get; }
    DbSet<SpecialCharge> SpecialCharges { get; }
    DbSet<FineRecord> FineRecords { get; }
    DbSet<MaintenanceBill> MaintenanceBills { get; }
    DbSet<MaintenanceBillItem> MaintenanceBillItems { get; }
    DbSet<MaintenancePayment> MaintenancePayments { get; }

    DbSet<Member> Members { get; }
    DbSet<FlatResidency> FlatResidencies { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<VehicleScanLog> VehicleScanLogs { get; }
    DbSet<EmergencyContact> EmergencyContacts { get; }
    DbSet<FlatResaleListing> FlatResaleListings { get; }

    DbSet<Event> Events { get; }
    DbSet<EventRsvp> EventRsvps { get; }

    DbSet<Gate> Gates { get; }
    DbSet<VisitorPurpose> VisitorPurposes { get; }
    DbSet<Visitor> Visitors { get; }
    DbSet<VisitorVisit> VisitorVisits { get; }
    DbSet<VisitorSettings> VisitorSettings { get; }

    DbSet<Person> People { get; }
    DbSet<FlatOccupancy> FlatOccupancies { get; }
    DbSet<OccupancyMember> OccupancyMembers { get; }
    DbSet<RentalAgreement> RentalAgreements { get; }
    DbSet<OccupancySettings> OccupancySettings { get; }

    DbSet<Staff> Staff { get; }
    DbSet<SocietyService> SocietyServices { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<Complaint> Complaints { get; }
    DbSet<CommitteeMember> CommitteeMembers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
