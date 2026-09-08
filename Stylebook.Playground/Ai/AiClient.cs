using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Stylebook.Playground.Ai;

/// <summary>
/// Quick-and-dirty AI client, specific to this temporary Playground app -
/// NOT the family's shared AiBrokerClient (that lives in Jabasoft.Base,
/// which is currently empty). Talks to whatever local OpenAI-compatible
/// server the user already has running (LM Studio on :1234, or Ollama
/// with its OpenAI-compat endpoint) - same provider convention as
/// JabaSoft.LocalAiStudio's AiConnector, just without its config UI,
/// per-task model split, or database-backed settings. Meant to be thrown
/// away and rebuilt against the real broker once Jabasoft.Base exists
/// again.
/// </summary>
public sealed class AiClient(string serverUrl, string model)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };

    /// <summary>
    /// history is the conversation so far (role "user"/"assistant",
    /// oldest first), ending on the newest user question - lets a
    /// follow-up question ("maak 'm nog scherper") build on what the AI
    /// just answered instead of starting over each time.
    /// </summary>
    public async Task<string> AskAsync(string systemPrompt, IReadOnlyList<(string Role, string Content)> history, CancellationToken cancellationToken = default)
    {
        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        messages.AddRange(history.Select(turn => (object)new { role = turn.Role, content = turn.Content }));

        var requestBody = new { model, messages, temperature = 0.3 };

        using var response = await Http.PostAsJsonAsync(
            $"{serverUrl.TrimEnd('/')}/v1/chat/completions", requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
