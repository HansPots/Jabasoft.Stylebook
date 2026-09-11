namespace Shared.Telemetry;

/// <summary>
/// A single recorded LLM call's token usage - a log (one row per call), not
/// a running total, so usage can be charted over time. Shared across every
/// JabaSoft app writing to the same JabasoftBase database:
/// <see cref="Application"/> identifies the source. Rows are never updated
/// after insert, so CreatedAtUtc (see <see cref="IAuditableEntity"/>) is
/// also the moment this call happened - no separate Timestamp field needed.
/// </summary>
public sealed class TokenUsageEntry : IAuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Which app recorded this (e.g. "Stylebook", "TabStudio", "LocalAiStudio").</summary>
    public string Application { get; set; } = string.Empty;

    /// <summary>The model name, if known (e.g. "qwen2.5-coder:14b").</summary>
    public string? Model { get; set; }

    public long PromptTokens { get; set; }

    public long CompletionTokens { get; set; }

    public long TotalTokens { get; set; }

    // Audit fields last, by convention - see feedback_db_audit_timestamps
    // memory (most important fields first, date/timestamp fields last).
    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
