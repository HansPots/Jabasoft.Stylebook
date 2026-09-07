using System.Text.Json;
using Jabasoft.Base.AiBroker;
using Stylebook.Web;

var builder = WebApplication.CreateBuilder(args);

// Runs as its own process (started by Jabasoft.App's EnsureAppsRunningAsync,
// same "dotnet run" pattern as TabStudio.Web/LocalAiStudio.Web), embedded
// full-window in Jabasoft's shell via the Apps:Stylebook config entry - see
// Jabasoft/Jabasoft.App/appsettings.json.
builder.Services.AddHttpClient();
// Timeout matches Jabasoft.Broker's own "chat" HttpClient (see its
// Program.cs) - without this, the default 100s HttpClient.Timeout cuts
// the request off well before the broker's 5-minute allowance for a slow/
// cold local model actually elapses.
builder.Services.AddHttpClient<IAiBrokerClient, AiBrokerClient>(c =>
{
    c.BaseAddress = new Uri(AiBrokerClient.DefaultBaseUrl);
    c.Timeout = TimeSpan.FromMinutes(5);
});

// Starts Jabasoft.Broker if no instance is reachable yet (any JabaSoft app
// can be the one that starts it) - fire-and-forget so a cold broker build
// doesn't delay Stylebook's own startup.
_ = AiBrokerProcessLauncher.EnsureRunningAsync();

var app = builder.Build();

// Same anti-clickjacking override TabStudio.Web/LocalAiStudio.Web use -
// Stylebook is embedded in the Jabasoft shell's <iframe>, a different
// origin (https://app.jabasoft.local, a WebView2 virtual host).
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.Remove("X-Frame-Options");
        context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self' https://app.jabasoft.local";
        return Task.CompletedTask;
    });

    await next();
});

app.UseDefaultFiles();
// Stylebook's wwwroot (styleguide.js/css, index.html) changes constantly
// during development - Kestrel's static file middleware doesn't send any
// Cache-Control header by default, so browsers apply their own heuristic
// freshness and can keep serving a stale <script>/<link> for a long time
// even across a full page reload or a brand new tab, regardless of what
// the server actually has on disk now. no-store forces every request to
// hit the server fresh.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-store",
});

// jabasoft-theme.css is edited here directly on disk (Shared.UI is a
// sibling project in this repo) - the *page* itself still loads it through
// the normal _content/Shared.UI/jabasoft-theme.css static-web-asset link
// (see wwwroot/index.html), so a save here is immediately live everywhere,
// no copy involved.
var themeFolder = builder.Configuration["SharedUi:ThemeFolder"]
    ?? Path.Combine(app.Environment.ContentRootPath, "..", "Shared.UI", "wwwroot");
var themeCssPath = Path.Combine(Path.GetFullPath(themeFolder), "jabasoft-theme.css");

// Components/ai-connector.json/dummy-pages are all read/written relative to
// ContentRootPath, which - unlike Jabasoft.App's old WebView2/build-output
// split - already points at this project's *source* folder when launched
// via "dotnet run" (same as every other JabaSoft Blazor app), so this data
// is automatically git-trackable without any extra source-vs-output config.
var componentsFolder = Path.Combine(app.Environment.ContentRootPath, "App_Data", "components");
Directory.CreateDirectory(componentsFolder);
var componentsIndexPath = Path.Combine(componentsFolder, "index.json");
var aiConnectorPath = Path.Combine(app.Environment.ContentRootPath, "ai-connector.json");
var jabasoftBaseProjectPath = builder.Configuration["JabasoftBaseProject:ProjectPath"]
    ?? Path.Combine(app.Environment.ContentRootPath, "..", "..", "Jabasoft.Base");

// Dummy-page copies are disposable snapshots, regenerated on demand by
// "Pagina's verversen" - but a copy captured before a code change can
// silently keep pointing at a path that no longer resolves. Clearing them
// on every startup means what you see always reflects the current code, at
// the cost of one re-capture click.
var dummyPagesFolder = Path.Combine(app.Environment.WebRootPath, "dummy-pages");
if (Directory.Exists(dummyPagesFolder))
{
    Directory.Delete(dummyPagesFolder, recursive: true);
}

app.MapGet("/api/apps-config", (IConfiguration configuration) => Results.Ok(BuildAppsConfig(configuration)));

app.MapGet("/api/theme-css", async () =>
{
    if (!File.Exists(themeCssPath))
    {
        return Results.NotFound();
    }

    return Results.Text(await File.ReadAllTextAsync(themeCssPath), "text/css");
});

app.MapPut("/api/theme-css", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var content = await reader.ReadToEndAsync();
    await File.WriteAllTextAsync(themeCssPath, content);
    return Results.Ok();
});

// ---------- components (select-in-preview -> named, separately-stylable
// component -> optionally materialized as a real Blazor component in
// Jabasoft.Base) ----------

app.MapGet("/api/components", async () => Results.Ok(await ReadComponentIndexAsync(componentsIndexPath)));

app.MapPost("/api/components", async (ComponentCreateRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest("Geef het component een naam.");
    }

    var safeName = ToSafeComponentName(request.Name);
    await WriteFileWithRetryAsync(Path.Combine(componentsFolder, $"{safeName}.html"), request.Html ?? string.Empty);

    // A new component starts from the real, currently-applied CSS of the
    // element the user pointed at (see styleguide.js's snapshotComputedCss)
    // rather than blank - that's the whole reason "Selecteer element"
    // targets a live element instead of asking for hand-typed markup. A
    // re-save under an existing name (picking the same thing again) never
    // touches the CSS file already there, so in-progress edits survive.
    var cssPath = Path.Combine(componentsFolder, $"{safeName}.css");
    if (!File.Exists(cssPath))
    {
        await WriteFileWithRetryAsync(cssPath, request.StartingCss ?? string.Empty);
    }

    var group = string.IsNullOrWhiteSpace(request.Group) ? "Overig" : request.Group;
    var index = await ReadComponentIndexAsync(componentsIndexPath);
    index.RemoveAll(c => c.Name.Equals(safeName, StringComparison.OrdinalIgnoreCase));
    index.Add(new ComponentInfo(safeName, request.SourceApp ?? "", request.SourcePath ?? "", DateTimeOffset.UtcNow, group));
    await WriteFileWithRetryAsync(componentsIndexPath, JsonSerializer.Serialize(index));

    return Results.Ok(new { name = safeName });
});

app.MapGet("/api/components/{name}/html", async (string name) =>
{
    var path = Path.Combine(componentsFolder, $"{ToSafeComponentName(name)}.html");
    return File.Exists(path) ? Results.Text(await File.ReadAllTextAsync(path), "text/html") : Results.NotFound();
});

app.MapGet("/api/components/{name}/css", async (string name) =>
{
    var path = Path.Combine(componentsFolder, $"{ToSafeComponentName(name)}.css");
    return File.Exists(path) ? Results.Text(await File.ReadAllTextAsync(path), "text/css") : Results.NotFound();
});

app.MapPut("/api/components/{name}/css", async (string name, HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var content = await reader.ReadToEndAsync();
    await WriteFileWithRetryAsync(Path.Combine(componentsFolder, $"{ToSafeComponentName(name)}.css"), content);
    return Results.Ok();
});

app.MapDelete("/api/components/{name}", async (string name) =>
{
    var safeName = ToSafeComponentName(name);

    var index = await ReadComponentIndexAsync(componentsIndexPath);
    var removed = index.RemoveAll(c => c.Name.Equals(safeName, StringComparison.OrdinalIgnoreCase)) > 0;
    if (!removed)
    {
        return Results.NotFound();
    }

    await WriteFileWithRetryAsync(componentsIndexPath, JsonSerializer.Serialize(index));

    var htmlPath = Path.Combine(componentsFolder, $"{safeName}.html");
    var cssPath = Path.Combine(componentsFolder, $"{safeName}.css");
    if (File.Exists(htmlPath))
    {
        File.Delete(htmlPath);
    }
    if (File.Exists(cssPath))
    {
        File.Delete(cssPath);
    }

    return Results.Ok();
});

app.MapPost("/api/components/{name}/generate-css", async (string name, ComponentGenerateCssRequest request, IAiBrokerClient broker) =>
{
    var safeName = ToSafeComponentName(name);
    var htmlPath = Path.Combine(componentsFolder, $"{safeName}.html");
    if (!File.Exists(htmlPath))
    {
        return Results.NotFound();
    }

    var html = await File.ReadAllTextAsync(htmlPath);
    var settings = await ReadAiConnectorSettingsAsync(aiConnectorPath);
    var themeExcerpt = File.Exists(themeCssPath) ? await File.ReadAllTextAsync(themeCssPath) : "";
    // The tokens (colors/spacing) are declared once near the top of the
    // shared stylesheet - a short excerpt is enough context for a model to
    // pick matching colors without pasting the whole file.
    var rootBlockEnd = themeExcerpt.IndexOf('}');
    var themeTokens = rootBlockEnd > 0 ? themeExcerpt[..(rootBlockEnd + 1)] : themeExcerpt;

    var hasCurrentCss = !string.IsNullOrWhiteSpace(request.CurrentCss);

    var systemPrompt =
        "Je schrijft CSS voor één UI-component van de JabaSoft-huisstijl. " +
        "Gebruik de opgegeven kleur-/spacing-tokens (CSS custom properties) waar passend. " +
        (hasCurrentCss
            ? "Er is al bestaande CSS voor dit component - gebruik die als basis en verander ALLEEN wat de instructie vraagt; laat elke andere regel/waarde ongewijzigd staan. Geef de volledige, bijgewerkte CSS terug (niet alleen het gewijzigde stuk). "
            : string.Empty) +
        "Antwoord ALLEEN met de CSS, geen uitleg, geen markdown-codeblok.";
    var userPrompt =
        $"HTML van het component:\n{html}\n\n" +
        (hasCurrentCss ? $"Huidige CSS van dit component (basis - alleen aanpassen wat gevraagd wordt):\n{request.CurrentCss}\n\n" : string.Empty) +
        $"Beschikbare tokens uit jabasoft-theme.css:\n{themeTokens}\n\n" +
        $"Instructies: {(string.IsNullOrWhiteSpace(request.Instructions) ? "maak een nette, opgeruimde stijl passend bij de huisstijl." : request.Instructions)}";

    var result = await broker.ChatAsync(
        new ChatRequest(
            ParseProvider(settings.Provider),
            settings.ServerUrl,
            settings.Model,
            [new ChatMessage("system", systemPrompt), new ChatMessage("user", userPrompt)],
            "Stylebook"),
        CancellationToken.None);

    if (!result.Success)
    {
        return Results.Ok(new { success = false, errorMessage = result.ErrorMessage });
    }

    return Results.Ok(new { success = true, css = StripMarkdownCodeFence(result.Reply) });
});

app.MapPost("/api/components/{name}/materialize", async (string name) =>
{
    var safeName = ToSafeComponentName(name);
    var htmlPath = Path.Combine(componentsFolder, $"{safeName}.html");
    var cssPath = Path.Combine(componentsFolder, $"{safeName}.css");
    if (!File.Exists(htmlPath))
    {
        return Results.NotFound();
    }

    var pascalName = ToPascalCase(safeName);
    var html = await File.ReadAllTextAsync(htmlPath);
    var css = File.Exists(cssPath) ? await File.ReadAllTextAsync(cssPath) : "";

    var razorPath = Path.Combine(Path.GetFullPath(jabasoftBaseProjectPath), $"{pascalName}.razor");
    var razorCssPath = Path.Combine(Path.GetFullPath(jabasoftBaseProjectPath), $"{pascalName}.razor.css");
    await WriteFileWithRetryAsync(razorPath, html);
    await WriteFileWithRetryAsync(razorCssPath, css);

    return Results.Ok(new { razorPath, razorCssPath, usageSnippet = $"<{pascalName} />" });
});

// ---------- AI Connector settings (LM Studio by default) - same shape as
// TabStudio's/LocalAiStudio's AiConnectorSettings, but Stylebook has no SQL
// database of its own, so it's a small runtime-writable JSON file next to
// the app instead. ----------

app.MapGet("/api/ai-connector", async () => Results.Ok(await ReadAiConnectorSettingsAsync(aiConnectorPath)));

app.MapPut("/api/ai-connector", async (AiConnectorSettings settings) =>
{
    await WriteFileWithRetryAsync(aiConnectorPath, JsonSerializer.Serialize(settings));
    return Results.Ok();
});

app.MapGet("/api/ai-models", async (string provider, string serverUrl, IAiBrokerClient broker) =>
    Results.Ok(await broker.ListModelsAsync(ParseProvider(provider), serverUrl, CancellationToken.None)));

app.MapPost("/api/ai-test-connection", async (AiConnectorSettings settings, IAiBrokerClient broker) =>
    Results.Ok(await broker.TestConnectionAsync(ParseProvider(settings.Provider), settings.ServerUrl, settings.Model, CancellationToken.None)));

// "Pagina's verversen": a live cross-origin embed can't be re-themed from
// here, so instead of embedding the live app, this fetches each configured
// page's current HTML *once* and saves it as a local copy under
// wwwroot/dummy-pages/ - same origin as Stylebook itself, styled with the
// normal <link>/theme.js setup (from the shared.jabasoft.local virtual
// host, not a local copy), no proxying at request time. These are frozen
// snapshots, not live views: rerun this after a real page's markup changes.
app.MapPost("/api/capture-pages", async (IConfiguration configuration, IHttpClientFactory httpClientFactory) =>
{
    using var client = httpClientFactory.CreateClient();
    client.Timeout = TimeSpan.FromSeconds(10);

    var captured = new List<object>();
    foreach (var appSection in configuration.GetSection("Apps").GetChildren())
    {
        var baseUrl = appSection["MainUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = appSection["DevelopmentUrl"];
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            continue;
        }

        var appFolder = Path.Combine(dummyPagesFolder, appSection.Key);
        Directory.CreateDirectory(appFolder);

        foreach (var pageSection in appSection.GetSection("Pages").GetChildren())
        {
            var path = pageSection["Path"];
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var fileName = ToSafeFileName(path) + ".html";
            try
            {
                var html = await client.GetStringAsync(baseUrl.TrimEnd('/') + path);
                var injection =
                    $"<base href=\"{baseUrl.TrimEnd('/')}/\" />" +
                    "<link rel=\"stylesheet\" href=\"https://shared.jabasoft.local/vs-theme.css\" />" +
                    "<script src=\"https://shared.jabasoft.local/theme.js\"></script>";

                var headIndex = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
                html = headIndex >= 0
                    ? html.Insert(headIndex + "<head>".Length, injection)
                    : injection + html;

                await WriteFileWithRetryAsync(Path.Combine(appFolder, fileName), html);
                captured.Add(new { app = appSection.Key, path, file = $"dummy-pages/{appSection.Key}/{fileName}", ok = true });
            }
            catch (Exception ex)
            {
                captured.Add(new { app = appSection.Key, path, error = ex.Message, ok = false });
            }
        }
    }

    return Results.Ok(captured);
});

app.Run();

/// <summary>
/// Reads the Apps section fresh from IConfiguration every call, so both
/// "Pagina's verversen" and the "Pagina's" tab's own page list reflect
/// appsettings.json edits without a restart.
/// </summary>
static Dictionary<string, object> BuildAppsConfig(IConfiguration configuration)
{
    var apps = new Dictionary<string, object>();
    foreach (var appSection in configuration.GetSection("Apps").GetChildren())
    {
        var pages = new List<object>();
        foreach (var pageSection in appSection.GetSection("Pages").GetChildren())
        {
            var path = pageSection["Path"];
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            pages.Add(new
            {
                path,
                label = pageSection["Label"] ?? path,
                file = $"dummy-pages/{appSection.Key}/{ToSafeFileName(path)}.html",
            });
        }

        apps[appSection.Key] = new
        {
            displayName = appSection["DisplayName"] ?? appSection.Key,
            developmentUrl = appSection["DevelopmentUrl"],
            mainUrl = appSection["MainUrl"],
            pages,
        };
    }

    return apps;
}

/// <summary>
/// A freshly-written file in this folder is occasionally still briefly
/// locked (observed with Windows file-system scanners) right after
/// creation, so a plain WriteAllTextAsync can spuriously fail here. Retried
/// a few times with a short backoff before giving up for real.
/// </summary>
static async Task WriteFileWithRetryAsync(string path, string content)
{
    for (var attempt = 1; attempt <= 3; attempt++)
    {
        try
        {
            await File.WriteAllTextAsync(path, content);
            return;
        }
        catch (IOException) when (attempt < 3)
        {
            await Task.Delay(200 * attempt);
        }
    }
}

static async Task<List<ComponentInfo>> ReadComponentIndexAsync(string indexPath)
{
    if (!File.Exists(indexPath))
    {
        return [];
    }

    var json = await File.ReadAllTextAsync(indexPath);
    return JsonSerializer.Deserialize<List<ComponentInfo>>(json) ?? [];
}

static async Task<AiConnectorSettings> ReadAiConnectorSettingsAsync(string path)
{
    if (!File.Exists(path))
    {
        await WriteFileWithRetryAsync(path, JsonSerializer.Serialize(AiConnectorSettings.Default));
        return AiConnectorSettings.Default;
    }

    var json = await File.ReadAllTextAsync(path);
    return JsonSerializer.Deserialize<AiConnectorSettings>(json) ?? AiConnectorSettings.Default;
}

static AiProvider ParseProvider(string? provider) =>
    Enum.TryParse<AiProvider>(provider, ignoreCase: true, out var parsed) ? parsed : AiProvider.LmStudio;

/// <summary>
/// Strips a markdown code fence a model sometimes wraps its answer in
/// (```css ... ```) despite being asked not to - kept lenient rather than
/// failing the request, since the CSS itself still lands in the editor for
/// the user to review either way.
/// </summary>
static string StripMarkdownCodeFence(string text)
{
    var trimmed = text.Trim();
    if (!trimmed.StartsWith("```", StringComparison.Ordinal))
    {
        return trimmed;
    }

    var firstNewline = trimmed.IndexOf('\n');
    if (firstNewline < 0)
    {
        return trimmed;
    }

    var withoutOpeningFence = trimmed[(firstNewline + 1)..];
    var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
    return (closingFenceIndex >= 0 ? withoutOpeningFence[..closingFenceIndex] : withoutOpeningFence).Trim();
}

/// <summary>Keeps letters/digits only (so it's safe as both a filename and a C# identifier); collapses everything else.</summary>
static string ToSafeComponentName(string name)
{
    var chars = name.Where(char.IsLetterOrDigit).ToArray();
    var safe = new string(chars);
    return string.IsNullOrEmpty(safe) ? "Component" : safe;
}

/// <summary>Capitalizes the first letter for use as a Razor component/class/file name.</summary>
static string ToPascalCase(string safeName) =>
    char.ToUpperInvariant(safeName[0]) + safeName[1..];

/// <summary>Turns a route like "/" or "/songs/{Id}" into a plain file-name-safe token ("root", "songs-id").</summary>
static string ToSafeFileName(string path)
{
    if (path == "/")
    {
        return "root";
    }

    var chars = path.Trim('/').ToCharArray();
    for (var i = 0; i < chars.Length; i++)
    {
        if (!char.IsLetterOrDigit(chars[i]))
        {
            chars[i] = '-';
        }
    }

    return new string(chars).ToLowerInvariant();
}
