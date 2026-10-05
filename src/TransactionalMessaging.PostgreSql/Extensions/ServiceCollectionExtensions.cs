using Microsoft.Extensions.DependencyInjection.Extensions;
using TransactionalMessaging.Core.Abstractions;
using TransactionalMessaging.PostgreSql.Inbox;
using TransactionalMessaging.PostgreSql.Outbox;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering PostgreSQL provider services.
/// </summary>
public static class PostgreSqlTransactionalMessagingExtensions
{
    /// <summary>
    /// Adds PostgreSQL stores for TransactionalMessaging.
    /// </summary>
    public static IServiceCollection AddPostgreSqlTransactionalMessaging(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton<IOutboxStore>(sp => new PostgreSqlOutboxStore(connectionString));
        services.TryAddSingleton<IInboxStore>(sp => new PostgreSqlInboxStore(connectionString));

        return services;
    }
}
