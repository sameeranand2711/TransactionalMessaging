using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TransactionalMessaging.Core.Abstractions;
using TransactionalMessaging.Hosting.Extensions;

namespace TransactionalMessaging.Tests.Integration;

public class DependencyInjectionTests
{
    [Fact]
    public void AddTransactionalMessaging_RegistersCoreServices()
    {
        var services = new ServiceCollection();

        services.AddTransactionalMessaging();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IMessageSerializer>());
        Assert.NotNull(provider.GetService<MessageValidator>());
    }

    [Fact]
    public void AddTransactionalMessaging_MultipleCallsDoNotDuplicateServices()
    {
        var services = new ServiceCollection();

        services.AddTransactionalMessaging();
        services.AddTransactionalMessaging();

        var provider = services.BuildServiceProvider();

        var serializers = provider.GetServices<IMessageSerializer>().ToList();
        Assert.Single(serializers);

        var validators = provider.GetServices<MessageValidator>().ToList();
        Assert.Single(validators);
    }

    [Fact]
    public void UseSqlServer_RegistersOutboxAndInboxStores()
    {
        var services = new ServiceCollection();
        const string connectionString = "Server=test;Database=test;";

        services.AddTransactionalMessaging()
            .UseSqlServer(connectionString);

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IOutboxStore>());
        Assert.NotNull(provider.GetService<IInboxStore>());
    }

    [Fact]
    public void UsePostgreSql_RegistersOutboxAndInboxStores()
    {
        var services = new ServiceCollection();
        const string connectionString = "Host=localhost;Database=test;";

        services.AddTransactionalMessaging()
            .UsePostgreSql(connectionString);

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IOutboxStore>());
        Assert.NotNull(provider.GetService<IInboxStore>());
    }

    [Fact]
    public void AddDispatcher_RegistersHostedServices()
    {
        var services = new ServiceCollection();
        const string connectionString = "Server=test;Database=test;";

        services.AddTransactionalMessaging()
            .UseSqlServer(connectionString)
            .AddDispatcher();

        var hostedServices = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();

        Assert.Contains(hostedServices, d => d.ImplementationType?.Name == "OutboxDispatcher");
        Assert.Contains(hostedServices, d => d.ImplementationType?.Name == "OutboxCleanupService");
    }

    [Fact]
    public void AddInboxCleanup_RegistersInboxCleanupService()
    {
        var services = new ServiceCollection();
        const string connectionString = "Server=test;Database=test;";

        services.AddTransactionalMessaging()
            .UseSqlServer(connectionString)
            .AddInboxCleanup();

        var hostedServices = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();

        Assert.Contains(hostedServices, d => d.ImplementationType?.Name == "InboxCleanupService");
    }

    [Fact]
    public void FullConfiguration_AllServicesRegistered()
    {
        var services = new ServiceCollection();
        const string connectionString = "Server=test;Database=test;";

        // Simulate a real application setup
        services.AddTransactionalMessaging()
            .UseSqlServer(connectionString)
            .AddDispatcher()
            .AddInboxCleanup();

        services.AddSingleton<IMessagePublisher, TestPublisher>();

        var provider = services.BuildServiceProvider();

        // Verify all required services are present
        Assert.NotNull(provider.GetRequiredService<IOutboxStore>());
        Assert.NotNull(provider.GetRequiredService<IInboxStore>());
        Assert.NotNull(provider.GetRequiredService<IMessageSerializer>());
        Assert.NotNull(provider.GetRequiredService<MessageValidator>());
        Assert.NotNull(provider.GetRequiredService<IMessagePublisher>());

        var hostedServices = provider.GetServices<IHostedService>().ToList();
        Assert.True(hostedServices.Count >= 3, "Expected at least 3 hosted services");
    }

    [Fact]
    public void ServiceLifetimes_AreCorrect()
    {
        var services = new ServiceCollection();
        const string connectionString = "Server=test;Database=test;";

        services.AddTransactionalMessaging()
            .UseSqlServer(connectionString);

        // Stores should be singleton
        var outboxDescriptor = services.First(d => d.ServiceType == typeof(IOutboxStore));
        Assert.Equal(ServiceLifetime.Singleton, outboxDescriptor.Lifetime);

        var inboxDescriptor = services.First(d => d.ServiceType == typeof(IInboxStore));
        Assert.Equal(ServiceLifetime.Singleton, inboxDescriptor.Lifetime);

        // Core services should be singleton
        var serializerDescriptor = services.First(d => d.ServiceType == typeof(IMessageSerializer));
        Assert.Equal(ServiceLifetime.Singleton, serializerDescriptor.Lifetime);
    }

    private class TestPublisher : IMessagePublisher
    {
        public Task<PublicationResult> PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PublicationResult { Success = true, MessageId = message.MessageId });
        }
    }
}
