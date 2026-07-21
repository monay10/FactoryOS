using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Plugins.Forms.Engine.Events;
using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Notifications.Channels;
using FactoryOS.Plugins.Workflow.Notifications.Configuration;
using FactoryOS.Plugins.Workflow.Notifications.Diagnostics;
using FactoryOS.Plugins.Workflow.Notifications.Events;
using FactoryOS.Plugins.Workflow.Notifications.Execution;
using FactoryOS.Plugins.Workflow.Notifications.Integration;
using FactoryOS.Plugins.Workflow.Notifications.Localization;
using FactoryOS.Plugins.Workflow.Notifications.Persistence;
using FactoryOS.Plugins.Workflow.Tasks.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Notification engine</b> — the enterprise notification
/// infrastructure that subscribes to the workflow, human task, approval and forms engines' events and turns
/// them (and explicit requests) into queued, retried, multi-channel deliveries. It registers the runtime, its
/// default in-memory persistence and stores, the eight channel senders, the queue / dispatcher / retry pipeline,
/// and the integration subscribers — which are wired in as the source engines' event sinks so their events flow
/// into notifications. The source engines are composed (idempotently) but never modified.
/// </summary>
public static class NotificationEngineServiceCollectionExtensions
{
    /// <summary>Registers the notification engine and its default in-memory infrastructure.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddNotificationEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new NotificationEngineOptions());
        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Persistence, stores and seams (all default to in-memory).
        services.TryAddSingleton<INotificationRepository, InMemoryNotificationRepository>();
        services.TryAddSingleton<INotificationStore, InMemoryNotificationStore>();
        services.TryAddSingleton<INotificationHistoryRepository, InMemoryNotificationHistoryRepository>();
        services.TryAddSingleton<INotificationTemplateRepository, InMemoryNotificationTemplateRepository>();
        services.TryAddSingleton<INotificationEventSink, InMemoryNotificationEventSink>();
        services.TryAddSingleton<INotificationLocalizer, InMemoryNotificationLocalizer>();
        services.TryAddSingleton<INotificationDirectory, InMemoryNotificationDirectory>();
        services.TryAddSingleton<INotificationPreferenceStore, InMemoryNotificationPreferenceStore>();
        services.TryAddSingleton<INotificationSubscriptionStore, InMemoryNotificationSubscriptionStore>();
        services.TryAddSingleton<INotificationOutbox, InMemoryNotificationOutbox>();

        // The eight channel senders.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationChannelSender, EmailChannelSender>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationChannelSender, SmsChannelSender>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationChannelSender, PushChannelSender>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationChannelSender, TeamsChannelSender>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationChannelSender, SlackChannelSender>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationChannelSender, WebhookChannelSender>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationChannelSender, InAppChannelSender>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationChannelSender, SignalRChannelSender>());

        // Diagnostics and the execution pipeline.
        services.TryAddSingleton<NotificationMetrics>();
        services.TryAddSingleton<NotificationTemplateEngine>();
        services.TryAddSingleton<RecipientResolver>();
        services.TryAddSingleton<PreferenceResolver>();
        services.TryAddSingleton<NotificationRouter>();
        services.TryAddSingleton<NotificationDispatcher>();
        services.TryAddSingleton<NotificationQueue>();
        services.TryAddSingleton<NotificationRetryService>();
        services.TryAddSingleton<NotificationQueueProcessor>();
        services.TryAddSingleton<DeadLetterQueue>();
        services.TryAddSingleton<NotificationRuntime>();
        services.TryAddSingleton<NotificationEngine>();

        // Integration subscribers.
        services.TryAddSingleton<WorkflowNotificationSubscriber>();
        services.TryAddSingleton<HumanTaskNotificationSubscriber>();
        services.TryAddSingleton<ApprovalNotificationSubscriber>();
        services.TryAddSingleton<FormsNotificationSubscriber>();
        services.TryAddSingleton<GenericEventSubscriber>();

        // Wire the subscribers in as the source engines' event sinks. Registering these before composing the
        // engines means the engines' own TryAdd of their in-memory sinks is skipped, so their events flow here.
        services.TryAddSingleton<IWorkflowEventSink>(
            provider => provider.GetRequiredService<WorkflowNotificationSubscriber>());
        services.TryAddSingleton<IHumanTaskEventSink>(
            provider => provider.GetRequiredService<HumanTaskNotificationSubscriber>());
        services.TryAddSingleton<IApprovalEventSink>(
            provider => provider.GetRequiredService<ApprovalNotificationSubscriber>());
        services.TryAddSingleton<IFormEventSink>(
            provider => provider.GetRequiredService<FormsNotificationSubscriber>());

        // Compose the source engines (idempotently). AddApprovalEngine also registers the workflow engine.
        services.AddApprovalEngine();
        services.AddHumanTaskEngine();
        services.AddFormsEngine();

        return services;
    }

    /// <summary>Registers the notification engine, binding <see cref="NotificationEngineOptions"/> from configuration.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The configuration to bind engine options from.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddNotificationEngine(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new NotificationEngineOptions();
        configuration.GetSection(NotificationConstants.ConfigurationSection).Bind(options);
        services.TryAddSingleton(options);

        return services.AddNotificationEngine();
    }
}
