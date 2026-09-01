using Microsoft.EntityFrameworkCore;

namespace Shared.Telemetry;

public sealed class TokenUsageRepository(TelemetryDbContext dbContext) : ITokenUsageRepository
{
    public async Task RecordAsync(string application, string? model, long promptTokens, long completionTokens, CancellationToken cancellationToken)
    {
        dbContext.TokenUsageEntries.Add(new TokenUsageEntry
        {
            Id = Guid.NewGuid(),
            Application = application,
            Timestamp = DateTimeOffset.UtcNow,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TokenUsageEntry>> GetEntriesAsync(string application, DateTimeOffset since, CancellationToken cancellationToken)
    {
        return await dbContext.TokenUsageEntries
            .Where(e => e.Application == application && e.Timestamp >= since)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TokenUsageEntry>> GetAllEntriesAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        return await dbContext.TokenUsageEntries
            .Where(e => e.Timestamp >= since)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }
}
