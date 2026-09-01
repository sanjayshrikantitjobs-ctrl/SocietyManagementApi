using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Extensions;

namespace SocietyManagement.Application.Features.Festivals;

public class FestivalSponsorDto
{
    public int Id { get; set; }
    public int FestivalId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public SponsorshipType SponsorshipType { get; set; }
    public decimal PromisedAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal PendingAmount => PromisedAmount - ReceivedAmount;
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateSponsorCommand(
    int FestivalId, string CompanyName, string? ContactPerson, string? Phone, string? Email,
    SponsorshipType SponsorshipType, decimal PromisedAmount, decimal ReceivedAmount,
    string? LogoUrl, string? BannerUrl) : IRequest<int>;

public class CreateSponsorCommandValidator : AbstractValidator<CreateSponsorCommand>
{
    public CreateSponsorCommandValidator()
    {
        RuleFor(x => x.FestivalId).GreaterThan(0);
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).Must(p => p!.IsValidIndianMobile()).When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PromisedAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReceivedAmount).GreaterThanOrEqualTo(0);
    }
}

public record UpdateSponsorCommand(
    int Id, string CompanyName, string? ContactPerson, string? Phone, string? Email,
    SponsorshipType SponsorshipType, decimal PromisedAmount, decimal ReceivedAmount,
    string? LogoUrl, string? BannerUrl) : IRequest<Unit>;

public class UpdateSponsorCommandValidator : AbstractValidator<UpdateSponsorCommand>
{
    public UpdateSponsorCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).Must(p => p!.IsValidIndianMobile()).When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PromisedAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReceivedAmount).GreaterThanOrEqualTo(0);
    }
}

public record DeleteSponsorCommand(int Id) : IRequest<Unit>;

public class FestivalSponsorCommandHandlers :
    IRequestHandler<CreateSponsorCommand, int>,
    IRequestHandler<UpdateSponsorCommand, Unit>,
    IRequestHandler<DeleteSponsorCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FestivalSponsorCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateSponsorCommand request, CancellationToken ct)
    {
        if (!await _context.Festivals.AnyAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Festival), request.FestivalId);
        }

        var sponsor = new FestivalSponsor
        {
            FestivalId = request.FestivalId,
            CompanyName = request.CompanyName,
            ContactPerson = request.ContactPerson,
            Phone = request.Phone,
            Email = request.Email,
            SponsorshipType = request.SponsorshipType,
            PromisedAmount = request.PromisedAmount,
            ReceivedAmount = request.ReceivedAmount,
            LogoUrl = request.LogoUrl,
            BannerUrl = request.BannerUrl
        };
        await _context.FestivalSponsors.AddAsync(sponsor, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Festivals", nameof(FestivalSponsor), sponsor.Id.ToString(), ct: ct);
        return sponsor.Id;
    }

    public async Task<Unit> Handle(UpdateSponsorCommand request, CancellationToken ct)
    {
        var sponsor = await _context.FestivalSponsors.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalSponsor), request.Id);

        sponsor.CompanyName = request.CompanyName;
        sponsor.ContactPerson = request.ContactPerson;
        sponsor.Phone = request.Phone;
        sponsor.Email = request.Email;
        sponsor.SponsorshipType = request.SponsorshipType;
        sponsor.PromisedAmount = request.PromisedAmount;
        sponsor.ReceivedAmount = request.ReceivedAmount;
        sponsor.LogoUrl = request.LogoUrl;
        sponsor.BannerUrl = request.BannerUrl;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalSponsor), sponsor.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteSponsorCommand request, CancellationToken ct)
    {
        var sponsor = await _context.FestivalSponsors.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalSponsor), request.Id);

        sponsor.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Festivals", nameof(FestivalSponsor), sponsor.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetSponsorsQuery(int FestivalId) : IRequest<List<FestivalSponsorDto>>;

public class FestivalSponsorQueryHandlers : IRequestHandler<GetSponsorsQuery, List<FestivalSponsorDto>>
{
    private readonly IApplicationDbContext _context;

    public FestivalSponsorQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<FestivalSponsorDto>> Handle(GetSponsorsQuery request, CancellationToken ct) =>
        await _context.FestivalSponsors
            .Where(s => s.FestivalId == request.FestivalId && !s.IsDeleted)
            .Select(s => new FestivalSponsorDto
            {
                Id = s.Id,
                FestivalId = s.FestivalId,
                CompanyName = s.CompanyName,
                ContactPerson = s.ContactPerson,
                Phone = s.Phone,
                Email = s.Email,
                SponsorshipType = s.SponsorshipType,
                PromisedAmount = s.PromisedAmount,
                ReceivedAmount = s.ReceivedAmount,
                LogoUrl = s.LogoUrl,
                BannerUrl = s.BannerUrl
            })
            .OrderByDescending(s => s.PromisedAmount)
            .ToListAsync(ct);
}
