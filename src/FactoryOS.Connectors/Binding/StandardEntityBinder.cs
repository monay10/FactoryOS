using System.Globalization;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Binding;

/// <summary>
/// Default <see cref="IStandardEntityBinder"/> for the built-in Standard Model entities. Binding is
/// explicit (no reflection): each entity type reads its canonical fields from the record's values.
/// </summary>
public sealed class StandardEntityBinder : IStandardEntityBinder
{
    /// <inheritdoc />
    public Result<IStandardEntity> Bind(NormalizedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return record.EntityType switch
        {
            InventoryItem.Type => BindInventoryItem(record),
            MeterReading.Type => BindMeterReading(record),
            Asset.Type => BindAsset(record),
            WorkOrder.Type => BindWorkOrder(record),
            DirectoryUser.Type => BindDirectoryUser(record),
            DirectoryGroup.Type => BindDirectoryGroup(record),
            _ => Result.Failure<IStandardEntity>(Error.NotFound(
                "Connector.Bind.UnknownEntity",
                $"No Standard Model binder is registered for entity type '{record.EntityType}'.")),
        };
    }

    private static Result<IStandardEntity> BindInventoryItem(NormalizedRecord record)
    {
        var sku = String(record, nameof(InventoryItem.Sku));
        var name = String(record, nameof(InventoryItem.Name));
        if (sku is null || name is null)
        {
            return MissingRequired(InventoryItem.Type, "Sku, Name");
        }

        return Result.Success<IStandardEntity>(new InventoryItem
        {
            Tenant = record.Tenant,
            Sku = sku,
            Name = name,
            Quantity = Decimal(record, nameof(InventoryItem.Quantity)) ?? 0m,
            Unit = String(record, nameof(InventoryItem.Unit)) ?? string.Empty,
            Location = String(record, nameof(InventoryItem.Location)),
        });
    }

    private static Result<IStandardEntity> BindMeterReading(NormalizedRecord record)
    {
        var meterId = String(record, nameof(MeterReading.MeterId));
        var metric = String(record, nameof(MeterReading.Metric));
        if (meterId is null || metric is null)
        {
            return MissingRequired(MeterReading.Type, "MeterId, Metric");
        }

        return Result.Success<IStandardEntity>(new MeterReading
        {
            Tenant = record.Tenant,
            MeterId = meterId,
            Metric = metric,
            Value = Decimal(record, nameof(MeterReading.Value)) ?? 0m,
            Unit = String(record, nameof(MeterReading.Unit)) ?? string.Empty,
            ReadingAt = DateTime(record, nameof(MeterReading.ReadingAt)) ?? default,
        });
    }

    private static Result<IStandardEntity> BindAsset(NormalizedRecord record)
    {
        var code = String(record, nameof(Asset.Code));
        var name = String(record, nameof(Asset.Name));
        if (code is null || name is null)
        {
            return MissingRequired(Asset.Type, "Code, Name");
        }

        return Result.Success<IStandardEntity>(new Asset
        {
            Tenant = record.Tenant,
            Code = code,
            Name = name,
            Kind = String(record, nameof(Asset.Kind)) ?? string.Empty,
            Location = String(record, nameof(Asset.Location)),
            Status = String(record, nameof(Asset.Status)) ?? string.Empty,
        });
    }

    private static Result<IStandardEntity> BindWorkOrder(NormalizedRecord record)
    {
        var number = String(record, nameof(WorkOrder.Number));
        var title = String(record, nameof(WorkOrder.Title));
        if (number is null || title is null)
        {
            return MissingRequired(WorkOrder.Type, "Number, Title");
        }

        return Result.Success<IStandardEntity>(new WorkOrder
        {
            Tenant = record.Tenant,
            Number = number,
            Title = title,
            Status = String(record, nameof(WorkOrder.Status)) ?? string.Empty,
            AssetCode = String(record, nameof(WorkOrder.AssetCode)),
            DueAt = DateTime(record, nameof(WorkOrder.DueAt)),
        });
    }

    private static Result<IStandardEntity> BindDirectoryUser(NormalizedRecord record)
    {
        var username = String(record, nameof(DirectoryUser.Username));
        var displayName = String(record, nameof(DirectoryUser.DisplayName));
        if (username is null || displayName is null)
        {
            return MissingRequired(DirectoryUser.Type, "Username, DisplayName");
        }

        return Result.Success<IStandardEntity>(new DirectoryUser
        {
            Tenant = record.Tenant,
            Username = username,
            DisplayName = displayName,
            Email = String(record, nameof(DirectoryUser.Email)),
            Enabled = Bool(record, nameof(DirectoryUser.Enabled)) ?? true,
        });
    }

    private static Result<IStandardEntity> BindDirectoryGroup(NormalizedRecord record)
    {
        var groupName = String(record, nameof(DirectoryGroup.GroupName));
        var displayName = String(record, nameof(DirectoryGroup.DisplayName));
        if (groupName is null || displayName is null)
        {
            return MissingRequired(DirectoryGroup.Type, "GroupName, DisplayName");
        }

        return Result.Success<IStandardEntity>(new DirectoryGroup
        {
            Tenant = record.Tenant,
            GroupName = groupName,
            DisplayName = displayName,
            Description = String(record, nameof(DirectoryGroup.Description)),
        });
    }

    private static Result<IStandardEntity> MissingRequired(string entityType, string fields) =>
        Result.Failure<IStandardEntity>(Error.Validation(
            "Connector.Bind.MissingField",
            $"Entity '{entityType}' requires values for: {fields}."));

    private static string? String(NormalizedRecord record, string field) =>
        record.Values.TryGetValue(field, out var value) && value is not null
            ? System.Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;

    private static bool? Bool(NormalizedRecord record, string field) =>
        record.Values.TryGetValue(field, out var value) && value is not null
            ? System.Convert.ToBoolean(value, CultureInfo.InvariantCulture)
            : null;

    private static decimal? Decimal(NormalizedRecord record, string field) =>
        record.Values.TryGetValue(field, out var value) && value is not null
            ? System.Convert.ToDecimal(value, CultureInfo.InvariantCulture)
            : null;

    private static DateTimeOffset? DateTime(NormalizedRecord record, string field)
    {
        if (!record.Values.TryGetValue(field, out var value) || value is null)
        {
            return null;
        }

        return value is DateTimeOffset offset
            ? offset
            : DateTimeOffset.Parse(System.Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);
    }
}
