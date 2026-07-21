using System.Collections.Concurrent;
using FactoryOS.Plugins.Forms.Engine.Domain;

namespace FactoryOS.Plugins.Forms.Engine.Persistence;

/// <summary>The registry of form definitions, keyed by form key and version.</summary>
public interface IFormRepository
{
    /// <summary>Registers a definition (idempotent by key and version).</summary>
    /// <param name="definition">The definition to register.</param>
    /// <returns><see langword="true"/> when this key/version was not already registered.</returns>
    bool Register(FormDefinition definition);

    /// <summary>Gets a specific definition version.</summary>
    /// <param name="key">The form key.</param>
    /// <param name="version">The version.</param>
    /// <returns>The definition, or <see langword="null"/> when not registered.</returns>
    FormDefinition? Get(string key, FormVersion version);

    /// <summary>Gets the highest registered version of a definition.</summary>
    /// <param name="key">The form key.</param>
    /// <returns>The latest definition, or <see langword="null"/> when none is registered.</returns>
    FormDefinition? GetLatest(string key);

    /// <summary>Gets every registered definition.</summary>
    /// <returns>The definitions.</returns>
    IReadOnlyCollection<FormDefinition> All();
}

/// <summary>An in-memory <see cref="IFormRepository"/>.</summary>
public sealed class InMemoryFormRepository : IFormRepository
{
    private readonly ConcurrentDictionary<string, FormDefinition> _definitions = new(StringComparer.Ordinal);

    private static string KeyOf(string key, FormVersion version) => $"{key}@{version.Value}";

    /// <inheritdoc />
    public bool Register(FormDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var added = false;
        _definitions.AddOrUpdate(
            KeyOf(definition.Key, definition.Version),
            _ => { added = true; return definition; },
            (_, existing) => existing);
        return added;
    }

    /// <inheritdoc />
    public FormDefinition? Get(string key, FormVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _definitions.TryGetValue(KeyOf(key, version), out var definition) ? definition : null;
    }

    /// <inheritdoc />
    public FormDefinition? GetLatest(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _definitions.Values
            .Where(definition => string.Equals(definition.Key, key, StringComparison.Ordinal))
            .OrderByDescending(definition => definition.Version)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<FormDefinition> All() => _definitions.Values.ToArray();
}

/// <summary>The persistence store for form instances.</summary>
public interface IFormStore
{
    /// <summary>Saves an instance (insert or update by id).</summary>
    /// <param name="instance">The instance to save.</param>
    void Save(FormInstance instance);

    /// <summary>Gets an instance by id.</summary>
    /// <param name="id">The instance id.</param>
    /// <returns>The instance, or <see langword="null"/> when not found.</returns>
    FormInstance? Get(Guid id);

    /// <summary>Lists the instances of a tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The tenant's instances.</returns>
    IReadOnlyCollection<FormInstance> ListByTenant(string tenant);

    /// <summary>Lists the instances in a given state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The matching instances.</returns>
    IReadOnlyCollection<FormInstance> ListByState(FormInstanceState state);
}

/// <summary>An in-memory <see cref="IFormStore"/>. Instances are held by reference, so saves are updates.</summary>
public sealed class InMemoryFormStore : IFormStore
{
    private readonly ConcurrentDictionary<Guid, FormInstance> _instances = new();

    /// <inheritdoc />
    public void Save(FormInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instances[instance.Id] = instance;
    }

    /// <inheritdoc />
    public FormInstance? Get(Guid id) => _instances.TryGetValue(id, out var instance) ? instance : null;

    /// <inheritdoc />
    public IReadOnlyCollection<FormInstance> ListByTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _instances.Values
            .Where(instance => string.Equals(instance.Tenant, tenant, StringComparison.Ordinal))
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<FormInstance> ListByState(FormInstanceState state) =>
        _instances.Values.Where(instance => instance.State == state).ToArray();
}

/// <summary>A recorded publication of a form version.</summary>
/// <param name="FormKey">The form key.</param>
/// <param name="Version">The version.</param>
/// <param name="RecordedOnUtc">When the version was recorded.</param>
public sealed record FormVersionRecord(string FormKey, FormVersion Version, DateTimeOffset RecordedOnUtc);

/// <summary>Tracks the version history of each form key.</summary>
public interface IFormVersionRepository
{
    /// <summary>Records that a version of a form was registered.</summary>
    /// <param name="record">The version record.</param>
    void Append(FormVersionRecord record);

    /// <summary>Lists the recorded versions of a form key, oldest first.</summary>
    /// <param name="formKey">The form key.</param>
    /// <returns>The version records.</returns>
    IReadOnlyList<FormVersionRecord> Versions(string formKey);
}

/// <summary>An in-memory <see cref="IFormVersionRepository"/>.</summary>
public sealed class InMemoryFormVersionRepository : IFormVersionRepository
{
    private readonly ConcurrentDictionary<string, List<FormVersionRecord>> _versions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Append(FormVersionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var list = _versions.GetOrAdd(record.FormKey, _ => []);
        lock (list)
        {
            list.Add(record);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<FormVersionRecord> Versions(string formKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formKey);
        if (!_versions.TryGetValue(formKey, out var list))
        {
            return [];
        }

        lock (list)
        {
            return list.OrderBy(record => record.Version).ToArray();
        }
    }
}

/// <summary>The persistence store for captured form submissions.</summary>
public interface IFormSubmissionRepository
{
    /// <summary>Adds a submission snapshot.</summary>
    /// <param name="submission">The submission.</param>
    void Add(FormSubmission submission);

    /// <summary>Gets a submission by id.</summary>
    /// <param name="id">The submission id.</param>
    /// <returns>The submission, or <see langword="null"/> when not found.</returns>
    FormSubmission? Get(Guid id);

    /// <summary>Lists the submissions captured for a form instance, oldest first.</summary>
    /// <param name="formInstanceId">The form instance id.</param>
    /// <returns>The submissions.</returns>
    IReadOnlyList<FormSubmission> ListByInstance(Guid formInstanceId);
}

/// <summary>An in-memory <see cref="IFormSubmissionRepository"/>.</summary>
public sealed class InMemoryFormSubmissionRepository : IFormSubmissionRepository
{
    private readonly ConcurrentDictionary<Guid, FormSubmission> _submissions = new();

    /// <inheritdoc />
    public void Add(FormSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        _submissions[submission.Id] = submission;
    }

    /// <inheritdoc />
    public FormSubmission? Get(Guid id) => _submissions.TryGetValue(id, out var submission) ? submission : null;

    /// <inheritdoc />
    public IReadOnlyList<FormSubmission> ListByInstance(Guid formInstanceId) =>
        _submissions.Values
            .Where(submission => submission.FormInstanceId == formInstanceId)
            .OrderBy(submission => submission.SubmittedOnUtc)
            .ToArray();
}
