namespace FactoryOS.Persistence.Configuration;

/// <summary>The relational provider the persistence layer runs against.</summary>
public enum DatabaseProvider
{
    /// <summary>SQLite — the development and automated-test provider.</summary>
    Sqlite = 0,

    /// <summary>PostgreSQL — the production provider mandated by the FactoryOS Constitution.</summary>
    PostgreSql = 1,
}

/// <summary>Persistence-layer constants: the configuration section and the platform-wide relational defaults.</summary>
public static class PersistenceConstants
{
    /// <summary>The configuration section the persistence options bind from.</summary>
    public const string ConfigurationSection = "Persistence";

    /// <summary>The default command timeout, in seconds.</summary>
    public const int DefaultCommandTimeoutSeconds = 30;

    /// <summary>The default number of retries for a transient failure.</summary>
    public const int DefaultMaxRetryCount = 3;

    /// <summary>The migrations-history table name, kept stable across providers.</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>The default tenant schema.</summary>
    public const string DefaultSchema = "public";

    /// <summary>The default maximum length applied to unbounded string columns by the base configuration.</summary>
    public const int DefaultMaxStringLength = 512;

    /// <summary>The default precision applied to <see cref="decimal"/> columns.</summary>
    public const int DecimalPrecision = 18;

    /// <summary>The default scale applied to <see cref="decimal"/> columns.</summary>
    public const int DecimalScale = 2;
}

/// <summary>Bindable options that select and tune the relational store.</summary>
public sealed class PersistenceOptions
{
    /// <summary>Gets or sets the relational provider.</summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    /// <summary>Gets or sets the connection string for the selected provider.</summary>
    public string ConnectionString { get; set; } = "Data Source=factoryos.db";

    /// <summary>Gets or sets the command timeout, in seconds.</summary>
    public int CommandTimeoutSeconds { get; set; } = PersistenceConstants.DefaultCommandTimeoutSeconds;

    /// <summary>Gets or sets the number of retries for a transient failure (PostgreSQL only).</summary>
    public int MaxRetryCount { get; set; } = PersistenceConstants.DefaultMaxRetryCount;

    /// <summary>Gets or sets the assembly that holds EF Core migrations, when it differs from the context's assembly.</summary>
    public string? MigrationsAssembly { get; set; }

    /// <summary>Gets or sets a value indicating whether EF Core migrations are applied on startup.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    /// <summary>Gets or sets a value indicating whether detailed EF Core errors are surfaced (development only).</summary>
    public bool EnableDetailedErrors { get; set; }

    /// <summary>Gets or sets a value indicating whether sensitive EF Core data is logged (development only).</summary>
    public bool EnableSensitiveDataLogging { get; set; }
}
