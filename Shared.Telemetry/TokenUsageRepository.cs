using Microsoft.EntityFrameworkCore;

namespace Shared.Telemetry;

public sealed class TokenUsageRepository(TelemetryDbContext dbContext) : ITokenUsageRepository
{
    public async Task RecordAsync(string application, string? model, long promptTokens, long completionTokens, CancellationToken cancellationToken)
    {
        dbContext.TokenUsageEntries.Add(new TokenUsageEntry
        {
            Application = application,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TokenUsageEntry>> GetEntriesAsync(string application, DateTime sinceUtc, CancellationToken cancellationToken)
    {
        return await dbContext.TokenUsageEntries
            .Where(e => e.Application == application && e.CreatedAtUtc >= sinceUtc)
            .OrderBy(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TokenUsageEntry>> GetAllEntriesAsync(DateTime sinceUtc, CancellationToken cancellationToken)
    {
        return await dbContext.TokenUsageEntries
            .Where(e => e.CreatedAtUtc >= sinceUtc)
            .OrderBy(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
