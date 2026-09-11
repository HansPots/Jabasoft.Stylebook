namespace Shared.Telemetry;

/// <summary>Records and reads back LLM token usage in the shared telemetry database.</summary>
public interface ITokenUsageRepository
{
    /// <summary>Records one LLM call's token usage, timestamped now (UTC) via CreatedAtUtc.</summary>
    Task RecordAsync(string application, string? model, long promptTokens, long completionTokens, CancellationToken cancellationToken);

    /// <summary>All entries for <paramref name="application"/> created since <paramref name="sinceUtc"/>, oldest first - for later charting.</summary>
    Task<IReadOnlyList<TokenUsageEntry>> GetEntriesAsync(string application, DateTime sinceUtc, CancellationToken cancellationToken);

    /// <summary>All entries across every application created since <paramref name="sinceUtc"/>, oldest first - for a cross-app overview (e.g. the Jabasoft hub dashboard).</summary>
    Task<IReadOnlyList<TokenUsageEntry>> GetAllEntriesAsync(DateTime sinceUtc, CancellationToken cancellationToken);
}
