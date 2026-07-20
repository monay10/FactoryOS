using FactoryOS.Domain.Abstractions;
using FactoryOS.Persistence.Auditing;
using FactoryOS.Persistence.Configuration;
using FactoryOS.Persistence.Multitenancy;
using FactoryOS.Persistence.Repositories;
using FactoryOS.Shared.Identifiers;
using FactoryOS.Shared.ValueObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Persistence;

/// <summary>
/// The persistence-foundation additions end-to-end on SQLite: the value converters and strongly-typed identifiers
/// round-trip through the store, the read repository queries without tracking, the base configuration and conventions
/// build a valid model, the provider selection honours the options, and <c>AddFactoryOsDbContext</c> composes the
/// repositories and units of work.
/// </summary>
public sealed class PersistenceFoundationTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 07, 20, 09, 00, 00, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly AuditingSaveChangesInterceptor _interceptor;
    private readonly ITenantSchemaProvider _schema = new FixedTenantSchemaProvider("public");

    public PersistenceFoundationTests()
    {
        _interceptor = new AuditingSaveChangesInterceptor(new FixedClock(Now), new SystemActorProvider());
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    private FoundationTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FoundationTestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_interceptor)
            .Options;
        return new FoundationTestDbContext(options, _schema);
    }

    private static Gizmo NewGizmo(MachineId id, string name) => Gizmo.Create(
        id,
        name,
        Money.Of(2500.75m, "usd"),
        Percentage.FromPercent(87.5m),
        DateRange.Between(Now, Now.AddDays(30)),
        GizmoStatus.Retired);

    [Fact]
    public async Task Value_objects_and_a_strongly_typed_id_round_trip_through_the_store()
    {
        var id = MachineId.New();

        await using (var context = CreateContext())
        {
            await context.Gizmos.AddAsync(NewGizmo(id, "Lathe"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var gizmo = await new ReadRepository<Gizmo, MachineId>(context).GetByIdAsync(id);

            Assert.NotNull(gizmo);
            Assert.Equal(id, gizmo!.Id);
            Assert.Equal(2500.75m, gizmo.Price.Amount);
            Assert.Equal("USD", gizmo.Price.Currency);
            Assert.Equal(87.5m, gizmo.Efficiency.Percent);
            Assert.Equal(Now, gizmo.Window.Start);
            Assert.Same(GizmoStatus.Retired, gizmo.Status);
            Assert.Equal(Now, gizmo.CreatedOnUtc);
            Assert.Equal("system", gizmo.CreatedBy);
        }
    }

    [Fact]
    public async Task The_read_repository_queries_without_tracking()
    {
        await using (var context = CreateContext())
        {
            await context.Gizmos.AddAsync(NewGizmo(MachineId.New(), "Press"));
            await context.Gizmos.AddAsync(NewGizmo(MachineId.New(), "Drill"));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var repository = new ReadRepository<Gizmo, MachineId>(context);

            var all = await repository.ListAsync();
            var presses = await repository.ListAsync(gizmo => gizmo.Name == "Press");

            Assert.Equal(2, all.Count);
            Assert.Single(presses);
            Assert.True(await repository.AnyAsync(gizmo => gizmo.Name == "Drill"));
            Assert.Empty(context.ChangeTracker.Entries());
        }
    }

    [Fact]
    public void The_strongly_typed_id_maps_to_its_primitive_by_convention()
    {
        using var context = CreateContext();

        var idProperty = context.Model.FindEntityType(typeof(Gizmo))!.FindProperty(nameof(Gizmo.Id))!;

        Assert.NotNull(idProperty.GetValueConverter());
        Assert.Equal(typeof(Guid), idProperty.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void The_base_configuration_bounds_the_audit_actor_columns()
    {
        using var context = CreateContext();

        var createdBy = context.Model.FindEntityType(typeof(Gizmo))!.FindProperty(nameof(Gizmo.CreatedBy))!;

        Assert.Equal(256, createdBy.GetMaxLength());
    }

    [Theory]
    [InlineData(DatabaseProvider.Sqlite, "Sqlite")]
    [InlineData(DatabaseProvider.PostgreSql, "Npgsql")]
    public void UseFactoryOsDatabase_selects_the_configured_provider(DatabaseProvider provider, string expected)
    {
        var options = new PersistenceOptions
        {
            Provider = provider,
            ConnectionString = provider == DatabaseProvider.Sqlite
                ? "DataSource=:memory:"
                : "Host=localhost;Database=factoryos;Username=u;Password=p",
        };

        var builder = new DbContextOptionsBuilder<FoundationTestDbContext>();
        builder.UseFactoryOsDatabase<FoundationTestDbContext>(options);

        using var context = new FoundationTestDbContext(builder.Options, _schema);
        Assert.Contains(expected, context.Database.ProviderName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddFactoryOsDbContext_composes_the_repositories_and_units_of_work()
    {
        var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"factoryos-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddPersistence(new ConfigurationBuilder().Build());
            services.AddFactoryOsDbContext<FoundationTestDbContext>(new PersistenceOptions
            {
                Provider = DatabaseProvider.Sqlite,
                ConnectionString = $"Data Source={file}",
            });

            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var sp = scope.ServiceProvider;

            var context = sp.GetRequiredService<FoundationTestDbContext>();
            await context.Database.EnsureCreatedAsync();

            var repository = sp.GetRequiredService<IRepository<Gizmo, MachineId>>();
            var readRepository = sp.GetRequiredService<IReadRepository<Gizmo, MachineId>>();
            var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
            Assert.NotNull(sp.GetRequiredService<DbContext>());
            Assert.NotNull(sp.GetRequiredService<FactoryOS.Shared.Abstractions.IUnitOfWork>());

            var id = MachineId.New();
            await repository.AddAsync(NewGizmo(id, "Mill"));
            await unitOfWork.SaveChangesAsync();

            Assert.NotNull(await readRepository.GetByIdAsync(id));
        }
        finally
        {
            if (System.IO.File.Exists(file))
            {
                System.IO.File.Delete(file);
            }
        }
    }

    public void Dispose() => _connection.Dispose();
}
