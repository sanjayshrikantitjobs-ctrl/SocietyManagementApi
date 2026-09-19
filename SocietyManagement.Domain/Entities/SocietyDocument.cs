using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>Society-level document repository entry (bylaws, circulars,
/// meeting minutes, certificates...). Separate from ResidentDocument, which
/// is per-resident KYC.</summary>
public class SocietyDocument : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public SocietyDocumentCategory Category { get; set; }
    public string FileUrl { get; set; } = default!;
    public string? FileName { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DocumentVisibility Visibility { get; set; } = DocumentVisibility.AdminOnly;
}
