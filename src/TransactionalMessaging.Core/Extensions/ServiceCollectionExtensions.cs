using Microsoft.Extensions.DependencyInjection.Extensions;
using TransactionalMessaging.Core.Abstractions;
using TransactionalMessaging.Core.Options;
using TransactionalMessaging.Core.Serialization;
using TransactionalMessaging.Core.Validation;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering TransactionalMessaging core services.
/// </summary>
public static class TransactionalMessagingServiceCollectionExtensions
{
    /// <summary>
    /// Adds core TransactionalMessaging services to the service collection.
    /// Provider-specific stores and message publisher must be registered separately.
    /// </summary>
    public static IServiceCollection AddTransactionalMessagingCore(
        this IServiceCollection services,
        Action<MessageOptions>? configureMessage = null,
        Action<RetryOptions>? configureRetry = null,
        Action<DispatcherOptions>? configureDispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register options
        var messageOptions = new MessageOptions();
        configureMessage?.Invoke(messageOptions);
        services.TryAddSingleton(messageOptions);

        var retryOptions = new RetryOptions();
        configureRetry?.Invoke(retryOptions);
        services.TryAddSingleton(retryOptions);

        var dispatcherOptions = new DispatcherOptions();
        configureDispatcher?.Invoke(dispatcherOptions);
        services.TryAddSingleton(dispatcherOptions);

        // Register core services
        services.TryAddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.TryAddSingleton<MessageValidator>();

        return services;
    }

    /// <summary>
    /// Adds TransactionalMessaging core services with a fluent configuration builder.
    /// Use this method to chain provider and hosting registrations.
    /// </summary>
    public static TransactionalMessagingBuilder AddTransactionalMessaging(
        this IServiceCollection services,
        Action<MessageOptions>? configureMessage = null,
        Action<RetryOptions>? configureRetry = null,
        Action<DispatcherOptions>? configureDispatcher = null)
    {
        services.AddTransactionalMessagingCore(configureMessage, configureRetry, configureDispatcher);
        return new TransactionalMessagingBuilder(services);
    }
}

/// <summary>
/// Builder for configuring TransactionalMessaging services.
/// </summary>
public sealed class TransactionalMessagingBuilder
{
    public IServiceCollection Services { get; }

    public TransactionalMessagingBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }
}
