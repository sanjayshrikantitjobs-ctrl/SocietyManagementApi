using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Application.Features.Auth.Commands.Logout;

public record LogoutCommand(string RefreshToken) : IRequest<Unit>;

public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;
    private readonly IAuditService _auditService;

    public LogoutCommandHandler(IApplicationDbContext context, IDateTime dateTime, IAuditService auditService)
    {
        _context = context;
        _dateTime = dateTime;
        _auditService = auditService;
    }

    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(request.RefreshToken, out var tokenGuid))
        {
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == tokenGuid, cancellationToken);
            if (token is not null && token.RevokedAt is null)
            {
                token.RevokedAt = _dateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        await _auditService.LogAsync(AuditAction.Logout, "Auth", ct: cancellationToken);

        return Unit.Value;
    }
}
