using System.Globalization;
using System.Text;
using System.Text.Json;
using FactoryOS.Plugins.Workflow.Audit.Domain;

namespace FactoryOS.Plugins.Workflow.Audit.Execution;

/// <summary>The formats an audit export can be rendered in.</summary>
public enum AuditExportFormat
{
    /// <summary>Comma-separated values, for spreadsheets and auditors.</summary>
    Csv = 0,

    /// <summary>JSON, for machine consumption.</summary>
    Json = 1,
}

/// <summary>
/// Renders audit records for export. The hash and previous-hash of every record travel with the export, so
/// whoever receives it can verify the chain themselves rather than having to trust the exporting system — an
/// export that dropped the hashes would be a story about the trail rather than evidence of it.
/// </summary>
public sealed class AuditExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Renders records in the requested format.</summary>
    /// <param name="records">The records to export.</param>
    /// <param name="format">The format.</param>
    /// <returns>The rendered export.</returns>
    public string Export(IReadOnlyList<AuditRecord> records, AuditExportFormat format)
    {
        ArgumentNullException.ThrowIfNull(records);
        return format == AuditExportFormat.Json ? ToJson(records) : ToCsv(records);
    }

    private static string ToJson(IReadOnlyList<AuditRecord> records) => JsonSerializer.Serialize(
        records.Select(record => new
        {
            record.Id,
            record.Sequence,
            record.Tenant,
            Organization = record.Scope.Organization,
            OccurredOnUtc = record.OccurredOnUtc.ToUniversalTime(),
            RecordedOnUtc = record.RecordedOnUtc.ToUniversalTime(),
            Category = record.Category.ToString(),
            Action = record.Action.ToString(),
            Severity = record.Severity.ToString(),
            Result = record.Result.ToString(),
            Actor = new { record.Actor.Id, Kind = record.Actor.Kind.ToString() },
            Target = new { Type = record.Target.Type.ToString(), record.Target.Key, record.Target.Id },
            record.Correlation.CorrelationId,
            record.Correlation.TraceId,
            record.Correlation.SessionId,
            record.Correlation.RequestId,
            record.EventType,
            record.Message,
            record.Metadata,
            Tags = record.Tags.Select(tag => tag.Name),
            record.PreviousHash,
            record.Hash,
        }),
        JsonOptions);

    private static string ToCsv(IReadOnlyList<AuditRecord> records)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "Sequence,Tenant,OccurredOnUtc,Category,Action,Severity,Result,ActorId,ActorKind,TargetType,TargetKey,TargetId,CorrelationId,TraceId,SessionId,EventType,Message,PreviousHash,Hash");

        foreach (var record in records)
        {
            builder
                .Append(record.Sequence.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Escape(record.Tenant)).Append(',')
                .Append(record.OccurredOnUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append(record.Category).Append(',')
                .Append(record.Action).Append(',')
                .Append(record.Severity).Append(',')
                .Append(record.Result).Append(',')
                .Append(Escape(record.Actor.Id)).Append(',')
                .Append(record.Actor.Kind).Append(',')
                .Append(record.Target.Type).Append(',')
                .Append(Escape(record.Target.Key)).Append(',')
                .Append(Escape(record.Target.Id)).Append(',')
                .Append(Escape(record.Correlation.CorrelationId)).Append(',')
                .Append(Escape(record.Correlation.TraceId)).Append(',')
                .Append(Escape(record.Correlation.SessionId)).Append(',')
                .Append(Escape(record.EventType)).Append(',')
                .Append(Escape(record.Message)).Append(',')
                .Append(record.PreviousHash).Append(',')
                .Append(record.Hash);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    // A field containing a comma, a quote or a newline is quoted, and embedded quotes are doubled.
    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.AsSpan().IndexOfAny(",\"\r\n") < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
