using Microsoft.Data.Sqlite;
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

    /// <summary>EF Core's InMemory provider throws on Database.
    /// BeginTransactionAsync (it's a relational-only API), so any handler
    /// that opens a transaction — like FacilityBookingFeature/
    /// AssetBookingFeature's Serializable-isolation overlap/quantity guard —
    /// needs a real relational provider to test. SQLite's in-memory mode
    /// (kept alive via one open connection for the context's lifetime) is
    /// the lightweight standard choice for that; dispose the returned
    /// SqliteTestDatabase to close the connection.</summary>
    public static SqliteTestDatabase CreateSqlite()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        return new SqliteTestDatabase(connection, db);
    }
}

public sealed class SqliteTestDatabase : IDisposable
{
    public SqliteTestDatabase(SqliteConnection connection, ApplicationDbContext db)
    {
        Connection = connection;
        Db = db;
    }

    public SqliteConnection Connection { get; }
    public ApplicationDbContext Db { get; }

    public void Dispose()
    {
        Db.Dispose();
        Connection.Dispose();
    }
}
