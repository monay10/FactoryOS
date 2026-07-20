namespace FactoryOS.Application.Messaging;

/// <summary>
/// Marks a request that requires a permission. The authorization behavior reads <see cref="RequiredPermission"/> and
/// rejects the request when the current user does not hold it — enforcing authorization at the pipeline boundary.
/// </summary>
public interface IAuthorizedRequest
{
    /// <summary>Gets the permission the caller must hold to run this request.</summary>
    string RequiredPermission { get; }
}
