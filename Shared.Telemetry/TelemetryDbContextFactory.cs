using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shared.Telemetry;

/// <summary>
/// Creates <see cref="TelemetryDbContext"/> for design-time EF Core
/// tooling. Reads the connection string from
/// JABASOFT_TELEMETRY_CONNECTION_STRING when set (matches
/// ConnectionStrings:JabasoftBase in the consuming app's appsettings.json),
/// otherwise falls back to the default local SQL Server instance - same
/// pattern as Stylebook.Data's StylebookDbContextFactory.
/// </summary>
public sealed class TelemetryDbContextFactory : IDesignTimeDbContextFactory<TelemetryDbContext>
{
    private const string ConnectionStringEnvironmentVariable = "JABASOFT_TELEMETRY_CONNECTION_STRING";

    private const string DefaultConnectionString =
        "Server=localhost;Database=JabasoftBase;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";

    public TelemetryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DefaultConnectionString;
        }

        var options = new DbContextOptionsBuilder<TelemetryDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new TelemetryDbContext(options);
    }
}
