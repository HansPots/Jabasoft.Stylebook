namespace Stylebook.Data.Entities;

/// <summary>
/// Which Component (if any) occupies one Basis-slot (Region) of a Page -
/// one row per (Page, Region), see PageRegionConfiguration's unique
/// index. ComponentId is nullable: a region can be registered for a
/// page before anything is assigned to it yet.
/// </summary>
public class PageRegion : IAuditableEntity
{
    public int Id { get; set; }

    public int PageId { get; set; }

    public ComponentRegion Region { get; set; }

    public int? ComponentId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
