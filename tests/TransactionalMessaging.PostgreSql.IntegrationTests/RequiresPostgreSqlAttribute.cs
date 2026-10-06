using Xunit;

namespace TransactionalMessaging.PostgreSql.IntegrationTests;

/// <summary>
/// Trait to mark tests that require PostgreSQL.
/// These tests verify critical transaction atomicity contracts but are environment-dependent.
/// </summary>
public class RequiresPostgreSqlAttribute : FactAttribute
{
    public RequiresPostgreSqlAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRESQL_TEST_CONNECTION");
        if (string.IsNullOrEmpty(connectionString))
        {
            Skip = "PostgreSQL not available. Set POSTGRESQL_TEST_CONNECTION environment variable to enable these tests. " +
                   "Example: Host=localhost;Port=5432;Database=TransactionalMessagingTests;Username=postgres;Password=postgres";
        }
    }
}
