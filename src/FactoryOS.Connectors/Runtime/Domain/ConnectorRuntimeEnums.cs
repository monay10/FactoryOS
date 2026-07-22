namespace FactoryOS.Connectors.Runtime.Domain;

/// <summary>
/// The broad family a connector belongs to. A family groups the concrete
/// <see cref="ConnectorCategory"/> values so a caller can ask for "any database" without enumerating every
/// engine, and so a policy can be written once per family.
/// </summary>
public enum ConnectorType
{
    /// <summary>The family is not known.</summary>
    Unknown = 0,

    /// <summary>Business systems of record: ERP, CRM and their vendors.</summary>
    Business = 1,

    /// <summary>Shop-floor systems: MES, SCADA, PLC and industrial IoT.</summary>
    Industrial = 2,

    /// <summary>Relational and key-value data stores.</summary>
    Data = 3,

    /// <summary>Web service protocols.</summary>
    Web = 4,

    /// <summary>Message brokers and streaming transports.</summary>
    Messaging = 5,

    /// <summary>Identity directories.</summary>
    Directory = 6,

    /// <summary>File and mail transports.</summary>
    Transport = 7,

    /// <summary>Cloud platform services.</summary>
    Cloud = 8,
}

/// <summary>
/// The concrete kind of external system a connector speaks to. The category is <b>descriptive metadata</b>,
/// never a branch: no runtime code path reads a category to decide behaviour, because that would put a
/// vendor's name into the core. It exists so the catalogue can be filtered and a policy can be scoped.
/// </summary>
public enum ConnectorCategory
{
    /// <summary>The category was not declared.</summary>
    Unknown = 0,

    /// <summary>A generic enterprise resource planning system.</summary>
    Erp = 1,

    /// <summary>A customer relationship management system.</summary>
    Crm = 2,

    /// <summary>A manufacturing execution system.</summary>
    Mes = 3,

    /// <summary>A supervisory control and data acquisition system.</summary>
    Scada = 4,

    /// <summary>A programmable logic controller.</summary>
    Plc = 5,

    /// <summary>A generic industrial IoT device or hub.</summary>
    Iot = 6,

    /// <summary>A generic database.</summary>
    Database = 7,

    /// <summary>An HTTP/REST service.</summary>
    Rest = 8,

    /// <summary>A SOAP web service.</summary>
    Soap = 9,

    /// <summary>A GraphQL endpoint.</summary>
    GraphQl = 10,

    /// <summary>A gRPC service.</summary>
    Grpc = 11,

    /// <summary>An MQTT broker.</summary>
    Mqtt = 12,

    /// <summary>An OPC-UA server.</summary>
    OpcUa = 13,

    /// <summary>A Modbus TCP or RTU device.</summary>
    Modbus = 14,

    /// <summary>An LDAP directory.</summary>
    Ldap = 15,

    /// <summary>A Microsoft Active Directory domain.</summary>
    ActiveDirectory = 16,

    /// <summary>An SMTP mail relay.</summary>
    Smtp = 17,

    /// <summary>An SFTP server.</summary>
    Sftp = 18,

    /// <summary>An FTP server.</summary>
    Ftp = 19,

    /// <summary>A local or mounted file system.</summary>
    FileSystem = 20,

    /// <summary>A Microsoft Azure service.</summary>
    Azure = 21,

    /// <summary>An Amazon Web Services service.</summary>
    Aws = 22,

    /// <summary>A Google Cloud service.</summary>
    GoogleCloud = 23,

    /// <summary>An SAP system.</summary>
    Sap = 24,

    /// <summary>A Logo ERP installation.</summary>
    Logo = 25,

    /// <summary>A Microsoft SQL Server instance.</summary>
    SqlServer = 26,

    /// <summary>An Oracle Database instance.</summary>
    Oracle = 27,

    /// <summary>A PostgreSQL instance.</summary>
    PostgreSql = 28,

    /// <summary>A MySQL instance.</summary>
    MySql = 29,

    /// <summary>A Redis instance.</summary>
    Redis = 30,

    /// <summary>A RabbitMQ broker.</summary>
    RabbitMq = 31,

    /// <summary>An Apache Kafka cluster.</summary>
    Kafka = 32,

    /// <summary>A WebSocket endpoint.</summary>
    WebSocket = 33,
}

/// <summary>Maps a <see cref="ConnectorCategory"/> onto the <see cref="ConnectorType"/> family it belongs to.</summary>
public static class ConnectorCategories
{
    /// <summary>Gets every declared category.</summary>
    /// <returns>The categories, excluding <see cref="ConnectorCategory.Unknown"/>.</returns>
    public static IReadOnlyList<ConnectorCategory> All() =>
        [.. Enum.GetValues<ConnectorCategory>().Where(category => category != ConnectorCategory.Unknown)];

    /// <summary>Gets the family a category belongs to.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The family.</returns>
    public static ConnectorType TypeOf(ConnectorCategory category) => category switch
    {
        ConnectorCategory.Erp or ConnectorCategory.Crm or ConnectorCategory.Sap or ConnectorCategory.Logo =>
            ConnectorType.Business,
        ConnectorCategory.Mes or ConnectorCategory.Scada or ConnectorCategory.Plc or ConnectorCategory.Iot
            or ConnectorCategory.OpcUa or ConnectorCategory.Modbus => ConnectorType.Industrial,
        ConnectorCategory.Database or ConnectorCategory.SqlServer or ConnectorCategory.Oracle
            or ConnectorCategory.PostgreSql or ConnectorCategory.MySql or ConnectorCategory.Redis =>
            ConnectorType.Data,
        ConnectorCategory.Rest or ConnectorCategory.Soap or ConnectorCategory.GraphQl or ConnectorCategory.Grpc
            or ConnectorCategory.WebSocket => ConnectorType.Web,
        ConnectorCategory.Mqtt or ConnectorCategory.RabbitMq or ConnectorCategory.Kafka => ConnectorType.Messaging,
        ConnectorCategory.Ldap or ConnectorCategory.ActiveDirectory => ConnectorType.Directory,
        ConnectorCategory.Smtp or ConnectorCategory.Sftp or ConnectorCategory.Ftp or ConnectorCategory.FileSystem =>
            ConnectorType.Transport,
        ConnectorCategory.Azure or ConnectorCategory.Aws or ConnectorCategory.GoogleCloud => ConnectorType.Cloud,
        _ => ConnectorType.Unknown,
    };

    /// <summary>Lists the categories belonging to a family.</summary>
    /// <param name="type">The family.</param>
    /// <returns>The categories in that family.</returns>
    public static IReadOnlyList<ConnectorCategory> InFamily(ConnectorType type) =>
        [.. All().Where(category => TypeOf(category) == type)];
}

/// <summary>The lifecycle status of a single tenant-scoped connector instance.</summary>
public enum ConnectorStatus
{
    /// <summary>The instance exists but has not been started.</summary>
    Stopped = 0,

    /// <summary>The instance is starting.</summary>
    Starting = 1,

    /// <summary>The instance is started and accepting invocations.</summary>
    Running = 2,

    /// <summary>The instance is running but has recorded failures.</summary>
    Degraded = 3,

    /// <summary>The instance is stopping.</summary>
    Stopping = 4,

    /// <summary>The instance failed and will not accept invocations until it is started again.</summary>
    Faulted = 5,
}

/// <summary>
/// What kind of failure an invocation hit. The kind decides whether a retry could plausibly help, so it is
/// the only thing the retry engine reads — a caller never has to classify an error twice.
/// </summary>
public enum ConnectorErrorKind
{
    /// <summary>An unexpected failure of unknown nature; treated as permanent.</summary>
    Unknown = 0,

    /// <summary>A momentary failure that a later attempt may well survive.</summary>
    Transient = 1,

    /// <summary>The attempt did not finish inside its deadline.</summary>
    Timeout = 2,

    /// <summary>The call was refused because a rate limit was reached.</summary>
    Throttled = 3,

    /// <summary>The credential was missing, invalid or expired.</summary>
    Unauthorized = 4,

    /// <summary>The caller is known but not permitted to perform the operation.</summary>
    Forbidden = 5,

    /// <summary>The instance, operation or remote resource does not exist.</summary>
    NotFound = 6,

    /// <summary>The request itself is malformed; no attempt will ever succeed.</summary>
    Validation = 7,

    /// <summary>The remote system failed in a way that will not resolve on its own.</summary>
    Permanent = 8,

    /// <summary>The circuit is open, so the call was not attempted at all.</summary>
    CircuitOpen = 9,

    /// <summary>The caller cancelled the invocation.</summary>
    Cancelled = 10,
}

/// <summary>The state of a circuit protecting one connector operation.</summary>
public enum CircuitState
{
    /// <summary>Calls flow normally.</summary>
    Closed = 0,

    /// <summary>Calls are refused without being attempted.</summary>
    Open = 1,

    /// <summary>A single trial call is permitted to decide whether the remote system has recovered.</summary>
    HalfOpen = 2,
}

/// <summary>The shape of a credential a connector instance presents to its external system.</summary>
public enum ConnectorCredentialKind
{
    /// <summary>The external system needs no credential.</summary>
    None = 0,

    /// <summary>A user name and password.</summary>
    Basic = 1,

    /// <summary>A single opaque key.</summary>
    ApiKey = 2,

    /// <summary>A bearer token.</summary>
    BearerToken = 3,

    /// <summary>An X.509 client certificate.</summary>
    Certificate = 4,

    /// <summary>A database connection string.</summary>
    ConnectionString = 5,

    /// <summary>An OAuth client id and secret pair.</summary>
    OAuthClient = 6,
}

/// <summary>The distinct questions the runtime asks when it reports a connector instance's health.</summary>
public enum ConnectorHealthAspect
{
    /// <summary>Is the instance started at all?</summary>
    Liveness = 0,

    /// <summary>Is it able to accept an invocation right now?</summary>
    Readiness = 1,

    /// <summary>Is the external system it depends on answering?</summary>
    Dependency = 2,

    /// <summary>Does the loaded connector satisfy the version the instance requires?</summary>
    Version = 3,

    /// <summary>Does its credential still resolve?</summary>
    Credential = 4,
}
