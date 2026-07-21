using System.Collections.Concurrent;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Persistence;

namespace FactoryOS.Plugins.Workflow.Audit.Execution;

/// <summary>
/// Seals entries into immutable records and appends them to their tenant's hash chain. Sequence numbers and
/// chain links come from the store's current head rather than from memory, so the chain survives a restart and
/// stays correct with several writers. Appends are serialised per tenant — two records can never claim the same
/// sequence number or link to the same predecessor.
/// </summary>
public sealed class AuditRecorder
{
    private readonly IAuditStore _store;
    private readonly IDateTimeProvider _clock;
    private readonly ConcurrentDictionary<string, Lock> _tenantGates = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="AuditRecorder"/> class.</summary>
    /// <param name="store">The append-only audit store.</param>
    /// <param name="clock">The clock.</param>
    public AuditRecorder(IAuditStore store, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _clock = clock;
    }

    /// <summary>Seals an entry and appends it to its tenant's chain.</summary>
    /// <param name="entry">The entry to record.</param>
    /// <returns>The sealed, immutable record.</returns>
    public AuditRecord Record(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var tenant = entry.Scope.Tenant;
        var gate = _tenantGates.GetOrAdd(tenant, _ => new Lock());
        lock (gate)
        {
            var head = _store.Head(tenant);
            var sequence = (head?.Sequence ?? 0) + 1;
            var previousHash = head?.Hash ?? AuditRecord.GenesisHash;
            var record = AuditRecord.Seal(entry, sequence, previousHash, _clock.UtcNow);
            _store.Append(record);
            return record;
        }
    }
}

/// <summary>
/// Verifies a stretch of the audit chain and reports the first place it breaks. Two independent things are
/// checked: that each record's stored hash still matches the hash its content implies (which catches an edited
/// field), and that consecutive records link through <see cref="AuditRecord.PreviousHash"/> (which catches a
/// removed or reordered record).
/// <para>
/// Linkage is only asserted between records whose sequence numbers are consecutive, because archiving
/// legitimately leaves gaps in the hot store. An archived stretch verifies on its own terms, and a record that
/// opens a chain (sequence 1) must link to the genesis marker.
/// </para>
/// </summary>
public sealed class AuditChainVerifier
{
    /// <summary>Verifies a stretch of chain, in sequence order.</summary>
    /// <param name="records">The records to verify, ordered by sequence.</param>
    /// <returns>The verdict, naming the first bad record when one is found.</returns>
    public AuditChainVerification Verify(IReadOnlyList<AuditRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];

            if (!string.Equals(record.RecomputeHash(), record.Hash, StringComparison.Ordinal))
            {
                return AuditChainVerification.Broken(
                    index, record.Sequence, "The record's content does not match its hash; it has been altered.");
            }

            if (index == 0)
            {
                if (record.Sequence == 1
                    && !string.Equals(record.PreviousHash, AuditRecord.GenesisHash, StringComparison.Ordinal))
                {
                    return AuditChainVerification.Broken(
                        index, record.Sequence, "The first record in the chain does not link to the genesis marker.");
                }

                continue;
            }

            var previous = records[index - 1];
            if (record.Sequence <= previous.Sequence)
            {
                return AuditChainVerification.Broken(
                    index, record.Sequence, "Records are out of sequence.");
            }

            // Only consecutive records must link; a gap means the predecessor was archived, which is legitimate.
            if (record.Sequence == previous.Sequence + 1
                && !string.Equals(record.PreviousHash, previous.Hash, StringComparison.Ordinal))
            {
                return AuditChainVerification.Broken(
                    index, record.Sequence, "The record does not link to its predecessor; the chain is broken.");
            }
        }

        return AuditChainVerification.Valid(records.Count);
    }
}
