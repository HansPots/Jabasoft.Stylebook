namespace Stylebook.Data.Entities;

/// <summary>
/// Single-row app-wide settings - currently just which theme is active.
/// Always exactly one row; read/write it via StylebookDbContext.AppSettings
/// (e.g. .Single()), never by a hardcoded Id. Needed because DesignTokens
/// no longer implies a "current theme" now that every theme has its own
/// rows - something has to say which one the app is actually showing.
/// </summary>
public class AppSetting : IAuditableEntity
{
    public int Id { get; set; }

    public Theme CurrentTheme { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
