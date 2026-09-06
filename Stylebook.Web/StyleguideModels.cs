namespace Stylebook.Web;

/// <summary>A component saved from the Stijlgids's "Selecteer element" picker (see Program.cs's /api/components endpoints).</summary>
public sealed record ComponentInfo(string Name, string SourceApp, string SourcePath, DateTimeOffset CreatedAt);

public sealed record ComponentCreateRequest(string Name, string? Html, string? SourceApp, string? SourcePath);

public sealed record ComponentGenerateCssRequest(string? Instructions);

/// <summary>
/// Stylebook's own AI Connector settings - same shape as TabStudio's/
/// LocalAiStudio's AiConnectorSettings (Provider/ServerUrl/Model), stored
/// as a small JSON file (see Program.cs) since Stylebook has no SQL
/// database of its own to keep it in.
/// </summary>
public sealed record AiConnectorSettings(string Provider, string ServerUrl, string Model)
{
    public static AiConnectorSettings Default { get; } = new("LmStudio", "http://localhost:1234", "");
}
