namespace FactoryOS.Connectors.EntraId;

/// <summary>
/// Strongly-typed configuration for the Microsoft Entra ID connector. A new tenant directory is configuration
/// only: the Graph resource paths and the access token used to reach them. The <see cref="HttpClient"/> base
/// address points at the Graph endpoint.
/// </summary>
public sealed record EntraIdConnectorOptions
{
    /// <summary>Gets the Graph path (relative to the client base address) that lists users.</summary>
    public string UsersPath { get; init; } =
        "/v1.0/users?$select=id,userPrincipalName,displayName,mail,accountEnabled";

    /// <summary>Gets the Graph path (relative to the client base address) that lists groups.</summary>
    public string GroupsPath { get; init; } = "/v1.0/groups?$select=id,displayName,description";

    /// <summary>Gets the OAuth bearer access token; injected from a secret, never hard-coded.</summary>
    public string AccessToken { get; init; } = string.Empty;
}
