using FactoryOS.Persistence.Repositories;
using FactoryOS.Persistence.Transactions;
using FactoryOS.Persistence.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.IntegrationTests.Persistence;

public sealed class PersistenceIntegrationTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 07, 19, 08, 30, 00, TimeSpan.Zero);

    private readonly TestDatabase _database = new(new FixedClock(Now));

    [Fact]
    public async Task Add_then_get_by_id_persists_and_stamps_audit_and_concurrency()
    {
        var id = Guid.NewGuid();

        await using (var context = _database.CreateContext())
        {
            var repository = new EfRepository<Widget, Guid>(context);
            var unitOfWork = new EfUnitOfWork(context);
            await repository.AddAsync(Widget.Create(id, "Gearbox"));
            var written = await unitOfWork.SaveChangesAsync();
            Assert.Equal(1, written);
        }

        await using (var context = _database.CreateContext())
        {
            var repository = new EfRepository<Widget, Guid>(context);
            var widget = await repository.GetByIdAsync(id);

            Assert.NotNull(widget);
            Assert.Equal("Gearbox", widget!.Name);
            Assert.Equal(Now, widget.CreatedOnUtc);
            Assert.Equal("system", widget.CreatedBy);
            Assert.NotEqual(Guid.Empty, widget.ConcurrencyToken);
        }
    }

    [Fact]
    public async Task Update_sets_modification_audit()
    {
        var id = Guid.NewGuid();
        await Seed(id, "Before");

        await using (var context = _database.CreateContext())
        {
            var repository = new EfRepository<Widget, Guid>(context);
            var widget = await repository.GetByIdAsync(id);
            widget!.Rename("After");
            await new EfUnitOfWork(context).SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var widget = await new EfRepository<Widget, Guid>(context).GetByIdAsync(id);
            Assert.Equal("After", widget!.Name);
            Assert.Equal(Now, widget.ModifiedOnUtc);
        }
    }

    [Fact]
    public async Task Remove_soft_deletes_and_is_filtered_from_queries()
    {
        var id = Guid.NewGuid();
        await Seed(id, "Doomed");

        await using (var context = _database.CreateContext())
        {
            var repository = new EfRepository<Widget, Guid>(context);
            repository.Remove((await repository.GetByIdAsync(id))!);
            await new EfUnitOfWork(context).SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            Assert.Null(await new EfRepository<Widget, Guid>(context).GetByIdAsync(id));

            var raw = await context.Widgets.IgnoreQueryFilters().SingleAsync(w => w.Id == id);
            Assert.True(raw.IsDeleted);
            Assert.Equal(Now, raw.DeletedOnUtc);
        }
    }

    [Fact]
    public async Task Concurrent_update_of_a_stale_aggregate_is_rejected()
    {
        var id = Guid.NewGuid();
        await Seed(id, "Shared");

        await using var first = _database.CreateContext();
        await using var second = _database.CreateContext();
        var widgetA = await first.Widgets.SingleAsync(w => w.Id == id);
        var widgetB = await second.Widgets.SingleAsync(w => w.Id == id);

        widgetA.Rename("First");
        await first.SaveChangesAsync();

        widgetB.Rename("Second");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Transaction_rolls_back_on_failure()
    {
        var id = Guid.NewGuid();

        await using (var context = _database.CreateContext())
        {
            var executor = new EfTransactionalExecutor(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(async token =>
            {
                await context.Widgets.AddAsync(Widget.Create(id, "Ghost"), token);
                await context.SaveChangesAsync(token);
                throw new InvalidOperationException("boom");
            }));
        }

        await using (var context = _database.CreateContext())
        {
            Assert.Null(await new EfRepository<Widget, Guid>(context).GetByIdAsync(id));
        }
    }

    private async Task Seed(Guid id, string name)
    {
        await using var context = _database.CreateContext();
        await context.Widgets.AddAsync(Widget.Create(id, name));
        await context.SaveChangesAsync();
    }

    public void Dispose() => _database.Dispose();
}
