using Xunit;

namespace TransactionalMessaging.SqlServer.IntegrationTests;

/// <summary>
/// Trait to mark tests that require SQL Server LocalDB or external SQL Server instance.
/// These tests verify critical transaction atomicity contracts but are environment-dependent.
/// </summary>
public class RequiresSqlServerAttribute : FactAttribute
{
    public RequiresSqlServerAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_TEST_CONNECTION");
        if (string.IsNullOrEmpty(connectionString))
        {
            Skip = "SQL Server not available. Set SQLSERVER_TEST_CONNECTION environment variable to enable these tests. " +
                   "Example: Server=(localdb)\\mssqllocaldb;Database=TransactionalMessagingTests;Integrated Security=true;TrustServerCertificate=true";
        }
    }
}
