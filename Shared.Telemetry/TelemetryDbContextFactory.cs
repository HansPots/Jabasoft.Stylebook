using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shared.Telemetry;

/// <summary>
/// Creates <see cref="TelemetryDbContext"/> for design-time EF Core tooling
/// ("dotnet ef migrations add", "dotnet ef database update"). Reads the
/// connection string from JABASOFT_TELEMETRY_CONNECTION_STRING when set
/// (matches how a consuming app resolves it, e.g.
/// ConnectionStrings:JabaSoftTelemetry in appsettings.json), otherwise
/// falls back to the default local SQL Server instance.
/// </summary>
public sealed class TelemetryDbContextFactory : IDesignTimeDbContextFactory<TelemetryDbContext>
{
    private const string ConnectionStringEnvironmentVariable = "JABASOFT_TELEMETRY_CONNECTION_STRING";

    private const string DefaultConnectionString =
        "Server=localhost;Database=JabaSoftTelemetry;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";

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
