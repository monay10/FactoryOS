using System.Reflection;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Framework.Activation;

/// <summary>
/// Activates an already-resolved connector <see cref="Type"/> into an <see cref="IConnector"/> instance and
/// verifies its key. This is the in-process activation seam of the connector platform; it does not load
/// external DLLs.
/// </summary>
public interface IConnectorActivator
{
    /// <summary>Activates a connector type and checks its reported key matches the expectation.</summary>
    /// <param name="connectorType">A concrete type implementing <see cref="IConnector"/>.</param>
    /// <param name="expectedKey">The key the activated connector must report.</param>
    /// <returns>A successful result with the instance, or a failure describing why activation failed.</returns>
    Result<IConnector> Activate(Type connectorType, string expectedKey);
}

/// <summary>Default <see cref="IConnectorActivator"/> using the parameterless constructor of the connector type.</summary>
public sealed class ConnectorActivator : IConnectorActivator
{
    /// <inheritdoc />
    public Result<IConnector> Activate(Type connectorType, string expectedKey)
    {
        ArgumentNullException.ThrowIfNull(connectorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedKey);

        if (connectorType is not { IsClass: true, IsAbstract: false }
            || !typeof(IConnector).IsAssignableFrom(connectorType))
        {
            return Result.Failure<IConnector>(Error.Validation(
                "Connector.Activate.NotAConnector",
                $"Type '{connectorType.FullName}' is not a concrete IConnector implementation."));
        }

        IConnector? instance;
        try
        {
            instance = Activator.CreateInstance(connectorType) as IConnector;
        }
        catch (Exception exception) when (exception is MissingMethodException or TargetInvocationException)
        {
            return Result.Failure<IConnector>(Error.Failure(
                "Connector.Activate.Failed",
                $"Type '{connectorType.FullName}' could not be activated: {exception.Message}"));
        }

        if (instance is null)
        {
            return Result.Failure<IConnector>(Error.Failure(
                "Connector.Activate.Failed",
                $"Type '{connectorType.FullName}' did not activate to an IConnector instance."));
        }

        if (!string.Equals(instance.Key, expectedKey, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<IConnector>(Error.Validation(
                "Connector.Activate.KeyMismatch",
                $"The activated connector reports key '{instance.Key}' but '{expectedKey}' was expected."));
        }

        return Result.Success(instance);
    }
}
