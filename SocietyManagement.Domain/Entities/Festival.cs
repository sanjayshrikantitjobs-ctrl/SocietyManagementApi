using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>Aggregate root for the Festival & Event Management module. Every
/// festival (Ganesh Chaturthi, Diwali, AGM, ...) is an independent project
/// with its own budget, contributions, sponsors, expenses and vendors.</summary>
public class Festival : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string Name { get; set; } = default!;

    public int Year { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string? Description { get; set; }

    public string? BannerImageUrl { get; set; }

    public string? CoverPhotoUrl { get; set; }

    public string? Theme { get; set; }

    public FestivalStatus Status { get; set; } = FestivalStatus.Planning;

    public FestivalVisibility Visibility { get; set; } = FestivalVisibility.Public;

    public bool IsRecurring { get; set; }

    /// <summary>Links this year's instance back to last year's, so recurring
    /// festivals can be created by cloning and later compared in the archive.</summary>
    public int? ParentFestivalId { get; set; }
    public Festival? ParentFestival { get; set; }

    public ICollection<FestivalBudgetCategory> BudgetCategories { get; set; } = new List<FestivalBudgetCategory>();
    public ICollection<FestivalContribution> Contributions { get; set; } = new List<FestivalContribution>();
    public ICollection<FestivalSponsor> Sponsors { get; set; } = new List<FestivalSponsor>();
    public ICollection<FestivalExpense> Expenses { get; set; } = new List<FestivalExpense>();
    public ICollection<FestivalFlatTarget> FlatTargets { get; set; } = new List<FestivalFlatTarget>();
}
