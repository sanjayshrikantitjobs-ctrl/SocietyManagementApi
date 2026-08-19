using FluentValidation;
using MediatR;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Application.Features.Auth.Commands.VerifyOtp;

/// <summary>Generic OTP verification used for login-OTP and mobile/email
/// verification flows (distinct from ResetPasswordCommand, which consumes the
/// OTP and the new password together in one atomic step).</summary>
public record VerifyOtpCommand(string Destination, OtpPurpose Purpose, string OtpCode) : IRequest<bool>;

public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.Destination).NotEmpty();
        RuleFor(x => x.OtpCode).NotEmpty().Length(6);
    }
}

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, bool>
{
    private readonly IOtpService _otpService;

    public VerifyOtpCommandHandler(IOtpService otpService) => _otpService = otpService;

    public Task<bool> Handle(VerifyOtpCommand request, CancellationToken cancellationToken) =>
        _otpService.ValidateAsync(request.Destination, request.Purpose, request.OtpCode, cancellationToken);
}
