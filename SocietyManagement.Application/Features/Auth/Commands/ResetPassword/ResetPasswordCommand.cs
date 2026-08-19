using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Auth.Commands.ResetPassword;

/// <summary>Completes the forgot-password flow: validates the OTP sent by
/// ForgotPasswordCommand and, if correct, sets the new password.</summary>
public record ResetPasswordCommand(string Identifier, string OtpCode, string NewPassword) : IRequest<Unit>;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty();
        RuleFor(x => x.OtpCode).NotEmpty().Length(6);
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
    }
}

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IOtpService _otpService;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordCommandHandler(
        IApplicationDbContext context, IOtpService otpService, IPasswordHasher passwordHasher)
    {
        _context = context;
        _otpService = otpService;
        _passwordHasher = passwordHasher;
    }

    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();

        var isValidOtp = await _otpService.ValidateAsync(
            identifier, OtpPurpose.ForgotPassword, request.OtpCode, cancellationToken);

        if (!isValidOtp)
        {
            throw new BadRequestAppException("The OTP is invalid or has expired.");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => (u.Email == identifier || u.MobileNumber == identifier) && !u.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.User), identifier);

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.AccessFailedCount = 0;
        user.IsLocked = false;
        user.LockedUntil = null;

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
