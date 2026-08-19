namespace SocietyManagement.Application.Features.Auth.Common;

public class LoginResponseDto
{
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public UserProfileDto User { get; set; } = default!;
}

public class UserProfileDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string MobileNumber { get; set; } = default!;
    public string? ProfilePhotoUrl { get; set; }
    public string RoleName { get; set; } = default!;
    public List<string> Permissions { get; set; } = new();
    public bool MustChangePassword { get; set; }
}
