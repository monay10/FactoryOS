using FactoryOS.Infrastructure.Transactions;
using FactoryOS.Shared.Abstractions;

namespace FactoryOS.Tests.Infrastructure;

public sealed class TransactionTests
{
    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    [Fact]
    public async Task Commit_flushes_the_unit_of_work()
    {
        var uow = new FakeUnitOfWork();
        var manager = new TransactionManager(uow);

        await using var transaction = await manager.BeginAsync();
        await transaction.CommitAsync();

        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task Rollback_leaves_the_unit_of_work_unflushed()
    {
        var uow = new FakeUnitOfWork();
        var manager = new TransactionManager(uow);

        await using var transaction = await manager.BeginAsync();
        await transaction.RollbackAsync();

        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public async Task A_disposed_transaction_reports_completion()
    {
        var manager = new TransactionManager(new FakeUnitOfWork());

        var transaction = (Transaction)await manager.BeginAsync();
        await transaction.CommitAsync();

        Assert.True(transaction.IsCompleted);
    }
}
