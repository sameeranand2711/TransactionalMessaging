using Microsoft.Extensions.DependencyInjection.Extensions;
using TransactionalMessaging.Core.Abstractions;
using TransactionalMessaging.SqlServer.Inbox;
using TransactionalMessaging.SqlServer.Outbox;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SQL Server provider services.
/// </summary>
public static class SqlServerTransactionalMessagingExtensions
{
    /// <summary>
    /// Adds SQL Server stores for TransactionalMessaging.
    /// </summary>
    public static IServiceCollection AddSqlServerTransactionalMessaging(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton<IOutboxStore>(sp => new SqlServerOutboxStore(connectionString));
        services.TryAddSingleton<IInboxStore>(sp => new SqlServerInboxStore(connectionString));

        return services;
    }
}
