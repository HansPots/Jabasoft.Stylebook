namespace Stylebook.Data.Entities;

public class StylebookComponent
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ComponentRegion Region { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
