namespace Stylebook.Data;

/// <summary>
/// Standing convention across every Jabasoft database: an entity that
/// tracks when it was created and last changed. StylebookDbContext stamps
/// both automatically in SaveChanges/SaveChangesAsync - implementing
/// entities never set these themselves.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; set; }

    DateTime UpdatedAtUtc { get; set; }
}
