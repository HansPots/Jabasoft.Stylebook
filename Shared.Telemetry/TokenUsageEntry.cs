namespace Shared.Telemetry;

/// <summary>
/// A single recorded LLM call's token usage, timestamped so usage can be
/// charted over time. Deliberately a log (one row per call), not a running
/// total: unlike JabaSoftLocalAiStudio's original single-counter
/// TokenUsageTotal, this table is shared across every JabaSoft app writing
/// to the same database, so <see cref="Application"/> identifies the
/// source and a per-row <see cref="Timestamp"/> is what makes a usage
/// graph possible at all.
/// </summary>
public sealed class TokenUsageEntry
{
    public Guid Id { get; set; }

    /// <summary>Which app recorded this (e.g. "TabStudio", "LocalAiStudio").</summary>
    public string Application { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    /// <summary>The model name, if known (e.g. "qwen2.5-coder:14b").</summary>
    public string? Model { get; set; }

    public long PromptTokens { get; set; }

    public long CompletionTokens { get; set; }

    public long TotalTokens { get; set; }
}
