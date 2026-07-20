using System.Data.Common;

namespace FactoryOS.Connectors.Sql;

/// <summary>
/// Creates provider-specific database connections for the SQL connector. Abstracting the factory keeps
/// the connector provider-agnostic (PostgreSQL, SQL Server, SQLite, …) — the door to the outside is the
/// connector, not a hardcoded driver.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Creates a new, unopened database connection.</summary>
    /// <returns>A new <see cref="DbConnection"/>.</returns>
    DbConnection Create();
}
