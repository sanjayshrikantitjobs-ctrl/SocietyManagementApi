using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Infrastructure.Identity;

/// <summary>BCrypt work factor 12 — deliberately slow to resist brute force,
/// per spec's "Password Hashing" security requirement.</summary>
public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
