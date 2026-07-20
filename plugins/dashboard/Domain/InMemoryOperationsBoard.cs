using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Dashboard.Domain;

/// <summary>
/// The default in-memory <see cref="IOperationsBoard"/>. State is partitioned by tenant — there is no code path
/// that reads or writes across tenants — and each tenant's board is guarded by its own lock, so concurrent
/// event handlers stay consistent. A production deployment would back this with Redis; the contract is identical.
/// </summary>
public sealed class InMemoryOperationsBoard : IOperationsBoard
{
    private sealed class TenantBoard
    {
        public Lock Gate { get; } = new();

        public Dictionary<string, OeeTile> Machines { get; } = new(StringComparer.Ordinal);

        public LinkedList<AlertTile> Alerts { get; } = new();
    }

    private readonly ConcurrentDictionary<string, TenantBoard> _boards = new(StringComparer.Ordinal);
    private readonly int _alertCapacity;

    /// <summary>Initializes a new instance of the <see cref="InMemoryOperationsBoard"/> class.</summary>
    /// <param name="options">The module options carrying the alert-feed capacity.</param>
    public InMemoryOperationsBoard(DashboardOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _alertCapacity = Math.Max(1, options.RecentAlertCapacity);
    }

    /// <inheritdoc />
    public void RecordOee(string tenant, OeeTile tile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var board = _boards.GetOrAdd(tenant, static _ => new TenantBoard());
        lock (board.Gate)
        {
            board.Machines[tile.MachineId] = tile;
        }
    }

    /// <inheritdoc />
    public void PushAlert(string tenant, AlertTile alert)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var board = _boards.GetOrAdd(tenant, static _ => new TenantBoard());
        lock (board.Gate)
        {
            board.Alerts.AddLast(alert);
            while (board.Alerts.Count > _alertCapacity)
            {
                board.Alerts.RemoveFirst();
            }
        }
    }

    /// <inheritdoc />
    public BoardSnapshot Snapshot(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        if (!_boards.TryGetValue(tenant, out var board))
        {
            return BoardSnapshot.Empty(tenant);
        }

        lock (board.Gate)
        {
            var machines = board.Machines.Values
                .OrderBy(static t => t.MachineId, StringComparer.Ordinal)
                .ToArray();

            // Newest first: the feed is stored oldest-to-newest, so reverse it for display.
            var alerts = new AlertTile[board.Alerts.Count];
            var index = alerts.Length - 1;
            foreach (var alert in board.Alerts)
            {
                alerts[index--] = alert;
            }

            var criticalCount = 0;
            foreach (var alert in alerts)
            {
                if (string.Equals(alert.Level, AlertLevels.Critical, StringComparison.Ordinal))
                {
                    criticalCount++;
                }
            }

            return new BoardSnapshot(tenant, machines, alerts, criticalCount);
        }
    }
}
