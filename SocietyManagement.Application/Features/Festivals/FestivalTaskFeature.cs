using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Festivals;

public class FestivalTaskDto
{
    public int Id { get; set; }
    public int FestivalId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public int? AssignedVolunteerId { get; set; }
    public string? AssignedVolunteerName { get; set; }
    public FestivalTaskStatus Status { get; set; }
    public DateTime? DueDate { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateTaskCommand(
    int FestivalId, string Title, string? Description, int? AssignedVolunteerId, DateTime? DueDate) : IRequest<int>;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.FestivalId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public record UpdateTaskCommand(
    int Id, string Title, string? Description, int? AssignedVolunteerId, FestivalTaskStatus Status, DateTime? DueDate) : IRequest<Unit>;

public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public record DeleteTaskCommand(int Id) : IRequest<Unit>;

public class FestivalTaskCommandHandlers :
    IRequestHandler<CreateTaskCommand, int>,
    IRequestHandler<UpdateTaskCommand, Unit>,
    IRequestHandler<DeleteTaskCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FestivalTaskCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    private async Task EnsureVolunteerBelongsToFestivalAsync(int festivalId, int? volunteerId, CancellationToken ct)
    {
        if (!volunteerId.HasValue) return;
        if (!await _context.FestivalVolunteers.AnyAsync(v => v.Id == volunteerId && v.FestivalId == festivalId && !v.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(FestivalVolunteer), volunteerId.Value);
        }
    }

    public async Task<int> Handle(CreateTaskCommand request, CancellationToken ct)
    {
        if (!await _context.Festivals.AnyAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Festival), request.FestivalId);
        }
        await EnsureVolunteerBelongsToFestivalAsync(request.FestivalId, request.AssignedVolunteerId, ct);

        var task = new FestivalTask
        {
            FestivalId = request.FestivalId, Title = request.Title, Description = request.Description,
            AssignedVolunteerId = request.AssignedVolunteerId, DueDate = request.DueDate, Status = FestivalTaskStatus.Pending
        };
        await _context.FestivalTasks.AddAsync(task, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Festivals", nameof(FestivalTask), task.Id.ToString(), ct: ct);
        return task.Id;
    }

    public async Task<Unit> Handle(UpdateTaskCommand request, CancellationToken ct)
    {
        var task = await _context.FestivalTasks.FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalTask), request.Id);
        await EnsureVolunteerBelongsToFestivalAsync(task.FestivalId, request.AssignedVolunteerId, ct);

        task.Title = request.Title;
        task.Description = request.Description;
        task.AssignedVolunteerId = request.AssignedVolunteerId;
        task.Status = request.Status;
        task.DueDate = request.DueDate;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalTask), task.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteTaskCommand request, CancellationToken ct)
    {
        var task = await _context.FestivalTasks.FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalTask), request.Id);

        task.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Festivals", nameof(FestivalTask), task.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetTasksQuery(int FestivalId) : IRequest<List<FestivalTaskDto>>;

public class FestivalTaskQueryHandlers : IRequestHandler<GetTasksQuery, List<FestivalTaskDto>>
{
    private readonly IApplicationDbContext _context;

    public FestivalTaskQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<FestivalTaskDto>> Handle(GetTasksQuery request, CancellationToken ct) =>
        await _context.FestivalTasks
            .Where(t => t.FestivalId == request.FestivalId && !t.IsDeleted)
            .Select(t => new FestivalTaskDto
            {
                Id = t.Id, FestivalId = t.FestivalId, Title = t.Title, Description = t.Description,
                AssignedVolunteerId = t.AssignedVolunteerId,
                AssignedVolunteerName = t.AssignedVolunteer != null ? t.AssignedVolunteer.Name : null,
                Status = t.Status, DueDate = t.DueDate
            })
            .OrderBy(t => t.Status).ThenBy(t => t.DueDate)
            .ToListAsync(ct);
}
