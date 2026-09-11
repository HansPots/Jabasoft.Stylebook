using System.Net.Http;
using Jabasoft.Base.AiBroker;

namespace Stylebook.Playground.Ai;

/// <summary>
/// Stylebook's thin wrapper around the family's shared
/// <see cref="IAiBrokerClient"/> - talks to Jabasoft.Broker instead of
/// calling LM Studio/Ollama directly, so token usage gets recorded
/// centrally and requests to the same local server queue instead of
/// racing other JabaSoft apps. Replaces the earlier quick-and-dirty,
/// Stylebook-only client that talked to the LLM server directly (thrown
/// away now that Jabasoft.Base/Jabasoft.Broker exist again).
/// </summary>
public sealed class AiClient(AiProvider provider, string serverUrl, string model)
{
    private const string ApplicationName = "Stylebook";

    private static readonly IAiBrokerClient Broker = new AiBrokerClient(new HttpClient
    {
        BaseAddress = new Uri(AiBrokerClient.DefaultBaseUrl),
        // Moet gelijk zijn aan (of langer dan) de Broker's eigen "chat"-
        // HttpClient-timeout (5 minuten, zie Jabasoft.Broker/Program.cs) -
        // de .NET-default van 100s knapt anders eerder af dan de Broker
        // een trage/koude lokale modelrespons mag laten duren.
        Timeout = TimeSpan.FromMinutes(5),
    });

    /// <summary>
    /// history is the conversation so far (role "user"/"assistant",
    /// oldest first), ending on the newest user question - lets a
    /// follow-up question ("maak 'm nog scherper") build on what the AI
    /// just answered instead of starting over each time.
    /// </summary>
    public async Task<string> AskAsync(string systemPrompt, IReadOnlyList<(string Role, string Content)> history, CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage> { new("system", systemPrompt) };
        messages.AddRange(history.Select(turn => new ChatMessage(turn.Role, turn.Content)));

        var request = new ChatRequest(provider, serverUrl, model, messages, ApplicationName, Temperature: 0.3);
        var result = await Broker.ChatAsync(request, cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "De AI-broker gaf een lege foutmelding terug.");
        }

        return result.Reply;
    }
}
