using System.Collections.ObjectModel;

namespace FactoryOS.Plugins.Forms.Engine.Domain;

/// <summary>
/// An immutable snapshot of the values a form instance was submitted with. Created only once validation has
/// passed; kept so a submission can be audited and read back independently of the mutable instance.
/// </summary>
public sealed class FormSubmission
{
    private FormSubmission(
        Guid id,
        Guid formInstanceId,
        string formKey,
        FormVersion version,
        string tenant,
        string? submittedBy,
        DateTimeOffset submittedOnUtc,
        IReadOnlyDictionary<string, object?> values)
    {
        Id = id;
        FormInstanceId = formInstanceId;
        FormKey = formKey;
        Version = version;
        Tenant = tenant;
        SubmittedBy = submittedBy;
        SubmittedOnUtc = submittedOnUtc;
        Values = values;
    }

    /// <summary>Gets the submission identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the form instance the submission came from.</summary>
    public Guid FormInstanceId { get; }

    /// <summary>Gets the form key.</summary>
    public string FormKey { get; }

    /// <summary>Gets the form version.</summary>
    public FormVersion Version { get; }

    /// <summary>Gets the owning tenant.</summary>
    public string Tenant { get; }

    /// <summary>Gets who submitted it, if known.</summary>
    public string? SubmittedBy { get; }

    /// <summary>Gets when it was submitted.</summary>
    public DateTimeOffset SubmittedOnUtc { get; }

    /// <summary>Gets the submitted values.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; }

    /// <summary>Captures a submission snapshot from an instance.</summary>
    /// <param name="id">The submission id.</param>
    /// <param name="instance">The submitted instance.</param>
    /// <param name="submittedOnUtc">When the submission occurred.</param>
    /// <returns>The submission snapshot.</returns>
    public static FormSubmission Capture(Guid id, FormInstance instance, DateTimeOffset submittedOnUtc)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var snapshot = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(instance.Values.AsReadOnly(), StringComparer.Ordinal));
        return new FormSubmission(
            id, instance.Id, instance.FormKey, instance.Version, instance.Tenant,
            instance.SubmittedBy, submittedOnUtc, snapshot);
    }
}
