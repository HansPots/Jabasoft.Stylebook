using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Stylebook.Data;

/// <summary>
/// Creates <see cref="StylebookDbContext"/> for design-time EF Core tooling.
/// Reads the connection string from JABASOFT_STYLEBOOK_CONNECTION_STRING
/// when set (matches ConnectionStrings:JabasoftStylebook in
/// appsettings.json), otherwise falls back to the default local SQL Server
/// instance - same pattern as JabaSoft.TabStudio's Data project.
/// </summary>
public sealed class StylebookDbContextFactory : IDesignTimeDbContextFactory<StylebookDbContext>
{
    private const string ConnectionStringEnvironmentVariable = "JABASOFT_STYLEBOOK_CONNECTION_STRING";

    private const string DefaultConnectionString =
        "Server=localhost;Database=JabasoftStylebook;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";

    public StylebookDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DefaultConnectionString;
        }

        var options = new DbContextOptionsBuilder<StylebookDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new StylebookDbContext(options);
    }
}
