using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Occupancy;

public class RentalAgreementDto
{
    public int Id { get; set; }
    public int FlatOccupancyId { get; set; }
    public DateTime AgreementStartDate { get; set; }
    public DateTime AgreementEndDate { get; set; }
    public decimal SecurityDeposit { get; set; }
    public decimal? RentAmount { get; set; }
    public PoliceVerificationStatus PoliceVerificationStatus { get; set; }
    public string? PoliceVerificationReference { get; set; }
    public string? AgreementDocumentUrl { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateRentalAgreementCommand(
    int FlatOccupancyId, DateTime AgreementStartDate, DateTime AgreementEndDate, decimal SecurityDeposit,
    decimal? RentAmount, PoliceVerificationStatus PoliceVerificationStatus, string? PoliceVerificationReference,
    string? AgreementDocumentUrl) : IRequest<int>;

public class CreateRentalAgreementCommandValidator : AbstractValidator<CreateRentalAgreementCommand>
{
    public CreateRentalAgreementCommandValidator()
    {
        RuleFor(x => x.FlatOccupancyId).GreaterThan(0);
        RuleFor(x => x.SecurityDeposit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RentAmount).GreaterThanOrEqualTo(0).When(x => x.RentAmount.HasValue);
    }
}

public record UpdateRentalAgreementCommand(
    int Id, DateTime AgreementStartDate, DateTime AgreementEndDate, decimal SecurityDeposit,
    decimal? RentAmount, PoliceVerificationStatus PoliceVerificationStatus, string? PoliceVerificationReference,
    string? AgreementDocumentUrl) : IRequest<Unit>;

public class UpdateRentalAgreementCommandValidator : AbstractValidator<UpdateRentalAgreementCommand>
{
    public UpdateRentalAgreementCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.SecurityDeposit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RentAmount).GreaterThanOrEqualTo(0).When(x => x.RentAmount.HasValue);
    }
}

public class RentalAgreementCommandHandlers :
    IRequestHandler<CreateRentalAgreementCommand, int>,
    IRequestHandler<UpdateRentalAgreementCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public RentalAgreementCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    private static void ValidateDates(DateTime start, DateTime end, FlatOccupancy occupancy)
    {
        if (end <= start)
        {
            throw new ConflictAppException("Agreement end date must be after the start date.");
        }
        if (start < occupancy.StartDate)
        {
            throw new ConflictAppException("Agreement cannot start before the tenant occupancy begins.");
        }
    }

    public async Task<int> Handle(CreateRentalAgreementCommand request, CancellationToken ct)
    {
        var occupancy = await _context.FlatOccupancies.FirstOrDefaultAsync(o => o.Id == request.FlatOccupancyId && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FlatOccupancy), request.FlatOccupancyId);

        if (occupancy.Type != OccupancyType.Tenant)
        {
            throw new ConflictAppException("A rental agreement can only be attached to a Tenant occupancy.");
        }
        if (occupancy.EndDate.HasValue)
        {
            throw new ConflictAppException("This occupancy has already ended.");
        }
        if (await _context.RentalAgreements.AnyAsync(r => r.FlatOccupancyId == request.FlatOccupancyId && !r.IsDeleted, ct))
        {
            throw new ConflictAppException("This occupancy already has a rental agreement.");
        }

        ValidateDates(request.AgreementStartDate, request.AgreementEndDate, occupancy);

        var agreement = new RentalAgreement
        {
            FlatOccupancyId = request.FlatOccupancyId, AgreementStartDate = request.AgreementStartDate,
            AgreementEndDate = request.AgreementEndDate, SecurityDeposit = request.SecurityDeposit,
            RentAmount = request.RentAmount, PoliceVerificationStatus = request.PoliceVerificationStatus,
            PoliceVerificationReference = request.PoliceVerificationReference, AgreementDocumentUrl = request.AgreementDocumentUrl
        };
        await _context.RentalAgreements.AddAsync(agreement, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Occupancy", nameof(RentalAgreement), agreement.Id.ToString(), ct: ct);
        return agreement.Id;
    }

    public async Task<Unit> Handle(UpdateRentalAgreementCommand request, CancellationToken ct)
    {
        var agreement = await _context.RentalAgreements.Include(r => r.FlatOccupancy)
            .FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(RentalAgreement), request.Id);

        if (agreement.FlatOccupancy.EndDate.HasValue)
        {
            throw new ConflictAppException("Cannot edit a rental agreement whose occupancy has already ended.");
        }

        ValidateDates(request.AgreementStartDate, request.AgreementEndDate, agreement.FlatOccupancy);

        agreement.AgreementStartDate = request.AgreementStartDate;
        agreement.AgreementEndDate = request.AgreementEndDate;
        agreement.SecurityDeposit = request.SecurityDeposit;
        agreement.RentAmount = request.RentAmount;
        agreement.PoliceVerificationStatus = request.PoliceVerificationStatus;
        agreement.PoliceVerificationReference = request.PoliceVerificationReference;
        agreement.AgreementDocumentUrl = request.AgreementDocumentUrl;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Occupancy", nameof(RentalAgreement), agreement.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}
