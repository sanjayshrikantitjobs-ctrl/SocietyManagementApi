using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Application.Features.Auth.Commands.ForgotPassword;

/// <summary>Kicks off the forgot-password flow by emailing/texting an OTP.
/// Always returns Unit (no "user not found" leak) to avoid account enumeration.</summary>
public record ForgotPasswordCommand(string Identifier) : IRequest<Unit>;

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() => RuleFor(x => x.Identifier).NotEmpty();
}

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IOtpService _otpService;

    public ForgotPasswordCommandHandler(IApplicationDbContext context, IOtpService otpService)
    {
        _context = context;
        _otpService = otpService;
    }

    public async Task<Unit> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => (u.Email == identifier || u.MobileNumber == identifier) && !u.IsDeleted,
                cancellationToken);

        // Deliberately silent if the user doesn't exist — prevents account enumeration.
        if (user is not null)
        {
            await _otpService.GenerateAndSendAsync(identifier, OtpPurpose.ForgotPassword, user.Id, cancellationToken);
        }

        return Unit.Value;
    }
}
