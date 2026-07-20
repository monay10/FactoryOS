using FactoryOS.Domain.Abstractions;
using FactoryOS.Persistence.Auditing;
using FactoryOS.Persistence.Multitenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.IntegrationTests.Persistence;

/// <summary>
/// A SQLite in-memory database shared across contexts (via a single kept-open connection), wired with
/// the FactoryOS auditing interceptor so integration tests exercise the real EF Core pipeline.
/// </summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AuditingSaveChangesInterceptor _interceptor;
    private readonly ITenantSchemaProvider _schema = new FixedTenantSchemaProvider("public");

    public TestDatabase(IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        Clock = clock;
        _interceptor = new AuditingSaveChangesInterceptor(clock, new SystemActorProvider());
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public IDateTimeProvider Clock { get; }

    public TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_interceptor)
            .Options;

        return new TestDbContext(options, _schema);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
