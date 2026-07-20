using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Persistence.Configuration;

/// <summary>Discovers and applies EF Core entity configurations from an assembly.</summary>
public static class EntityConfigurationDiscovery
{
    /// <summary>
    /// Applies every <see cref="IEntityTypeConfiguration{TEntity}"/> declared in the assembly that owns
    /// <typeparamref name="TMarker"/>. A module points this at any type in its assembly to register all of its
    /// entity configurations at once.
    /// </summary>
    /// <typeparam name="TMarker">A type in the assembly whose configurations should be applied.</typeparam>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance, to allow chaining.</returns>
    public static ModelBuilder ApplyConfigurationsFrom<TMarker>(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder.ApplyConfigurationsFromAssembly(typeof(TMarker).Assembly);
    }

    /// <summary>Applies every <see cref="IEntityTypeConfiguration{TEntity}"/> declared in an assembly.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="assembly">The assembly to scan for configurations.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance, to allow chaining.</returns>
    public static ModelBuilder ApplyConfigurationsFrom(this ModelBuilder modelBuilder, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(assembly);
        return modelBuilder.ApplyConfigurationsFromAssembly(assembly);
    }
}
