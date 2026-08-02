namespace FactoryOS.Plugins.Workflow.Security.Domain;

/// <summary>A single direct grant: a permission a subject holds in a tenant.</summary>
/// <param name="Subject">The subject that holds the permission.</param>
/// <param name="Permission">The granted permission, in the <c>resource.action</c> grammar.</param>
public sealed record SecurityGrantEntry(string Subject, string Permission);
