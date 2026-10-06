using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TransactionalMessaging.Hosting.Options;

namespace TransactionalMessaging.Hosting.Extensions;

/// <summary>
/// Extension methods for registering hosted dispatcher and cleanup services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the OutboxDispatcher as a hosted background service.
    /// Requires IOutboxStore and IMessagePublisher to be registered.
    /// </summary>
    public static IServiceCollection AddOutboxDispatcher(
        this IServiceCollection services,
        Action<CleanupOptions>? configureCleanup = null)
    {
        services.AddHostedService<OutboxDispatcher>();

        // Add cleanup service with optional configuration
        if (configureCleanup != null)
        {
            services.Configure(configureCleanup);
        }

        services.AddHostedService<OutboxCleanupService>();

        return services;
    }

    /// <summary>
    /// Adds the InboxCleanupService as a hosted background service.
    /// Requires IInboxStore to be registered.
    /// </summary>
    public static IServiceCollection AddInboxCleanup(
        this IServiceCollection services,
        Action<InboxCleanupOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddHostedService<InboxCleanupService>();

        return services;
    }

    /// <summary>
    /// Adds the OutboxDispatcher to the TransactionalMessaging builder.
    /// </summary>
    public static TransactionalMessagingBuilder AddDispatcher(
        this TransactionalMessagingBuilder builder,
        Action<CleanupOptions>? configureCleanup = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddOutboxDispatcher(configureCleanup);
        return builder;
    }

    /// <summary>
    /// Adds inbox cleanup to the TransactionalMessaging builder.
    /// </summary>
    public static TransactionalMessagingBuilder AddInboxCleanup(
        this TransactionalMessagingBuilder builder,
        Action<InboxCleanupOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddInboxCleanup(configure);
        return builder;
    }
}
