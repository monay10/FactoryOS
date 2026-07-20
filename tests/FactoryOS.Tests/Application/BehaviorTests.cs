using FactoryOS.Application.Behaviors;
using FactoryOS.Application.Configuration;
using FactoryOS.Application.Messaging;
using FactoryOS.Application.Services;
using FactoryOS.Application.Transactions;
using FactoryOS.Application.Validation;
using FactoryOS.Shared.Exceptions;
using FactoryOS.Shared.Identifiers;
using Microsoft.Extensions.Logging.Abstractions;

namespace FactoryOS.Tests.Application;

public sealed class ValidationBehaviorTests
{
    private sealed class FailingValidator : IValidator<string>
    {
        public Task<IValidationResult> ValidateAsync(string instance, CancellationToken cancellationToken = default) =>
            Task.FromResult<IValidationResult>(ValidationResult.Failure([new ValidationFailure("Name", "is required")]));
    }

    [Fact]
    public async Task Failing_validation_throws_before_the_handler_runs()
    {
        var validators = new IValidator<string>[] { new FailingValidator() };
        var behavior = new ValidationBehavior<string, int>(validators, new ApplicationOptions());

        await Assert.ThrowsAsync<ValidationException>(
            () => behavior.HandleAsync("x", () => Task.FromResult(1)));
    }

    [Fact]
    public async Task Validation_can_be_disabled()
    {
        var validators = new IValidator<string>[] { new FailingValidator() };
        var behavior = new ValidationBehavior<string, int>(validators, new ApplicationOptions { EnableValidation = false });

        var result = await behavior.HandleAsync("x", () => Task.FromResult(42));

        Assert.Equal(42, result);
    }
}

public sealed class AuthorizationBehaviorTests
{
    private sealed record GuardedCommand(string RequiredPermission) : ICommand, IAuthorizedRequest;

    private sealed class FakeUser(params string[] permissions) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public UserId? UserId => FactoryOS.Shared.Identifiers.UserId.New();

        public string? UserName => "tester";

        public IReadOnlyCollection<string> Permissions => permissions;

        public bool HasPermission(string permission) => permissions.Contains(permission);
    }

    [Fact]
    public async Task A_caller_without_the_permission_is_forbidden()
    {
        var behavior = new AuthorizationBehavior<GuardedCommand, int>(new FakeUser());

        await Assert.ThrowsAsync<ForbiddenException>(
            () => behavior.HandleAsync(new GuardedCommand("maintenance.close"), () => Task.FromResult(1)));
    }

    [Fact]
    public async Task A_caller_with_the_permission_proceeds()
    {
        var behavior = new AuthorizationBehavior<GuardedCommand, int>(new FakeUser("maintenance.close"));

        var result = await behavior.HandleAsync(new GuardedCommand("maintenance.close"), () => Task.FromResult(7));

        Assert.Equal(7, result);
    }
}

public sealed class TransactionBehaviorTests
{
    private sealed class FakeTransaction : ITransaction
    {
        public bool Committed { get; private set; }

        public bool RolledBack { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RolledBack = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeTransactionManager : ITransactionManager
    {
        public FakeTransaction Transaction { get; } = new();

        public bool Began { get; private set; }

        public Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            Began = true;
            return Task.FromResult<ITransaction>(Transaction);
        }
    }

    private sealed record SaveCommand : ICommand;

    private sealed record GetQuery : IQuery<int>;

    [Fact]
    public async Task A_command_commits_on_success()
    {
        var manager = new FakeTransactionManager();
        var behavior = new TransactionBehavior<SaveCommand, int>(manager);

        await behavior.HandleAsync(new SaveCommand(), () => Task.FromResult(1));

        Assert.True(manager.Transaction.Committed);
        Assert.False(manager.Transaction.RolledBack);
    }

    [Fact]
    public async Task A_failing_command_rolls_back()
    {
        var manager = new FakeTransactionManager();
        var behavior = new TransactionBehavior<SaveCommand, int>(manager);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.HandleAsync(new SaveCommand(), () => throw new InvalidOperationException("boom")));

        Assert.True(manager.Transaction.RolledBack);
        Assert.False(manager.Transaction.Committed);
    }

    [Fact]
    public async Task A_query_runs_outside_a_transaction()
    {
        var manager = new FakeTransactionManager();
        var behavior = new TransactionBehavior<GetQuery, int>(manager);

        var result = await behavior.HandleAsync(new GetQuery(), () => Task.FromResult(5));

        Assert.False(manager.Began);
        Assert.Equal(5, result);
    }
}

public sealed class LoggingBehaviorTests
{
    [Fact]
    public async Task The_response_passes_through()
    {
        var behavior = new LoggingBehavior<string, int>(NullLogger<LoggingBehavior<string, int>>.Instance);

        var result = await behavior.HandleAsync("x", () => Task.FromResult(99));

        Assert.Equal(99, result);
    }
}
