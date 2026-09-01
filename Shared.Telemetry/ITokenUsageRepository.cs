namespace Shared.Telemetry;

/// <summary>Records and reads back LLM token usage in the shared telemetry database.</summary>
public interface ITokenUsageRepository
{
    /// <summary>Records one LLM call's token usage, timestamped now (UTC).</summary>
    Task RecordAsync(string application, string? model, long promptTokens, long completionTokens, CancellationToken cancellationToken);

    /// <summary>All entries for <paramref name="application"/> in the given time range, oldest first - for later charting.</summary>
    Task<IReadOnlyList<TokenUsageEntry>> GetEntriesAsync(string application, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>All entries across every application in the given time range, oldest first - for a cross-app overview (e.g. the Jabasoft hub dashboard).</summary>
    Task<IReadOnlyList<TokenUsageEntry>> GetAllEntriesAsync(DateTimeOffset since, CancellationToken cancellationToken);
}
