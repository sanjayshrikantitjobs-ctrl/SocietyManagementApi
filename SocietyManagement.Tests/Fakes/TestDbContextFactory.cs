using Microsoft.EntityFrameworkCore;
using SocietyManagement.Infrastructure.Persistence;

namespace SocietyManagement.Tests.Fakes;

public static class TestDbContextFactory
{
    /// <summary>A fresh, isolated in-memory ApplicationDbContext per call —
    /// each test gets its own database (random name) so tests never leak
    /// state into one another.</summary>
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
