using System.CommandLine;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// Simple CLI tool that can ask either an OpenAI compatible GPT model or Google Gemini
// to generate event definitions that match the in-game GameEvent structure.

var rootCommand = BuildCommandLine();
rootCommand.Description = "Utility to generate Plague Inc Clone events using GPT or Google APIs.";

await rootCommand.InvokeAsync(args);

static RootCommand BuildCommandLine()
{
    var providerOption = new Option<EventProvider>(
        aliases: new[] { "--provider", "-p" },
        description: "Which LLM provider to use: gpt or google.",
        getDefaultValue: () => EventProvider.Gpt);

    var apiKeyOption = new Option<string>(
        aliases: new[] { "--apiKey", "-k" },
        description: "API key for the chosen provider. Falls back to environment variables.");

    var modelOption = new Option<string>(
        aliases: new[] { "--model", "-m" },
        description: "Model identifier (default is provider specific).");

    var countOption = new Option<int>(
        aliases: new[] { "--count", "-c" },
        () => 3,
        "How many events to request.");

    var outputOption = new Option<string>(
        aliases: new[] { "--output", "-o" },
        () => "events.generated.json",
        "Output path for the generated JSON.");

    var promptOption = new Option<string>(
        aliases: new[] { "--prompt" },
        description: "Additional creative direction for the model.");

    var systemPromptOption = new Option<string>(
        aliases: new[] { "--systemPrompt" },
        description: "Override the default system prompt template.");

    var dryRunOption = new Option<bool>(
        aliases: new[] { "--dryRun" },
        description: "Do not call any API. Prints the payload for inspection.");

    var root = new RootCommand
    {
        providerOption,
        apiKeyOption,
        modelOption,
        countOption,
        outputOption,
        promptOption,
        systemPromptOption,
        dryRunOption
    };

    root.SetHandler(async context =>
    {
        var provider = context.ParseResult.GetValueForOption(providerOption);
        var apiKey = context.ParseResult.GetValueForOption(apiKeyOption);
        var model = context.ParseResult.GetValueForOption(modelOption);
        var count = Math.Max(1, context.ParseResult.GetValueForOption(countOption));
        var output = context.ParseResult.GetValueForOption(outputOption) ?? "events.generated.json";
        var prompt = context.ParseResult.GetValueForOption(promptOption) ?? string.Empty;
        var systemPrompt = context.ParseResult.GetValueForOption(systemPromptOption);
        var dryRun = context.ParseResult.GetValueForOption(dryRunOption);

        EventGeneratorOptions options = new()
        {
            Provider = provider,
            ApiKey = apiKey,
            Model = model,
            EventCount = count,
            OutputPath = output,
            AdditionalPrompt = prompt,
            SystemPromptOverride = systemPrompt,
            DryRun = dryRun
        };

        try
        {
            await new EventGeneratorApp().RunAsync(options, context.GetCancellationToken());
            context.ExitCode = 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Event generation failed: " + ex.Message);
            context.ExitCode = 1;
        }
    });

    return root;
}

enum EventProvider
{
    Gpt,
    Google
}

sealed class EventGeneratorOptions
{
    public EventProvider Provider { get; set; } = EventProvider.Gpt;
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public int EventCount { get; set; } = 3;
    public string OutputPath { get; set; } = "events.generated.json";
    public string AdditionalPrompt { get; set; } = string.Empty;
    public string? SystemPromptOverride { get; set; }
    public bool DryRun { get; set; }
}

sealed class EventGeneratorApp
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task RunAsync(EventGeneratorOptions options, CancellationToken cancellationToken)
    {
        var provider = CreateProvider(options);
        var request = BuildRequest(options);

        if (options.DryRun)
        {
            Console.WriteLine("Dry run enabled. Payload that would be sent:");
            Console.WriteLine(provider.DumpRequestPayload(request));
            return;
        }

        string rawJson = await provider.GenerateEventsAsync(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(rawJson))
            throw new InvalidOperationException("LLM returned empty response.");

        List<GameEvent>? events;
        try
        {
            events = JsonSerializer.Deserialize<List<GameEvent>>(rawJson, JsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse JSON from model: " + ex.Message + "\nRaw:\n" + rawJson);
        }

        if (events == null || events.Count == 0)
            throw new InvalidOperationException("Model response did not contain any events.");

        string outputJson = JsonSerializer.Serialize(events, JsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath) ?? ".");
        await File.WriteAllTextAsync(options.OutputPath, outputJson, cancellationToken);

        Console.WriteLine($"Generated {events.Count} event(s) -> {options.OutputPath}");
    }

    static EventGenerationRequest BuildRequest(EventGeneratorOptions options)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("Create richly themed strategy events for a Plague Inc style game.");
        promptBuilder.AppendLine("Every event must match this JSON schema:");
        promptBuilder.AppendLine(EventJsonSchema);
        promptBuilder.AppendLine("Respect numeric ranges: Influence/Stability/Development deltas between -5 and 5.");
        promptBuilder.AppendLine("Sanity/Money/Artifacts costs or rewards should be believable and consistent.");
        promptBuilder.AppendLine("Scope meaning: Local -> impacts one region. Global -> affects every region.");
        promptBuilder.AppendLine("Personal -> only changes player resources and must list featuredPeople (name, position, gender) for the characters involved.");
        promptBuilder.AppendLine("Global events should not target a single region; leave region blank or treat it as All.");

        if (!string.IsNullOrWhiteSpace(options.AdditionalPrompt))
        {
            promptBuilder.AppendLine("Extra guidance: " + options.AdditionalPrompt);
        }

        return new EventGenerationRequest
        {
            Count = options.EventCount,
            Prompt = promptBuilder.ToString(),
            SystemPrompt = options.SystemPromptOverride ?? DefaultSystemPrompt
        };
    }

    static IEventGenerationProvider CreateProvider(EventGeneratorOptions options)
    {
        return options.Provider switch
        {
            EventProvider.Gpt => new OpenAiEventGenerator(options.ApiKey, options.Model),
            EventProvider.Google => new GoogleEventGenerator(options.ApiKey, options.Model),
            _ => throw new ArgumentOutOfRangeException(nameof(options.Provider))
        };
    }

    const string DefaultSystemPrompt =
        "You craft balanced strategic events for a management game. " +
        "Answer ONLY with valid JSON array matching the provided schema. " +
        "Do not include explanations or markdown. Ensure data stays lore friendly.";

    const string EventJsonSchema = @"
[
  {
    ""id"": 999,
    ""description"": ""Narrative hook"",
    ""scope"": ""Local | Global | Personal"",
    ""isBadEvent"": true,
    ""featuredPeople"": [
      {
        ""name"": ""Character name"",
        ""position"": ""Role or title"",
        ""gender"": ""Male | Female""
      }
    ],
    ""options"": [
      {
        ""text"": ""Player choice label"",
        ""sanityRequired"": 0,
        ""moneyRequired"": 0,
        ""artifactsRequired"": 0,
        ""sanityChange"": 0,
        ""moneyChange"": 0,
        ""artifactsChange"": 0,
        ""influenceChange"": 0,
        ""stabilityChange"": 0,
        ""developmentChange"": 0,
        ""minInfluenceRequired"": 0,
        ""minDevelopmentRequired"": 0,
        ""modifiers"": [
          {
            ""name"": ""Optional modifier name"",
            ""durationSeconds"": 15,
            ""tickIntervalSeconds"": 5,
            ""influencePerTick"": 0,
            ""stabilityPerTick"": 0,
            ""developmentPerTick"": 0
          }
        ]
      }
    ]
  }
]";
}

interface IEventGenerationProvider
{
    Task<string> GenerateEventsAsync(EventGenerationRequest request, CancellationToken cancellationToken);
    string DumpRequestPayload(EventGenerationRequest request);
}

sealed class EventGenerationRequest
{
    public int Count { get; init; }
    public string Prompt { get; init; } = string.Empty;
    public string SystemPrompt { get; init; } = string.Empty;
}

sealed class OpenAiEventGenerator : IEventGenerationProvider
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly HttpClient _httpClient;

    public OpenAiEventGenerator(string? apiKey, string? model)
    {
        _apiKey = ResolveApiKey(apiKey, "OPENAI_API_KEY");
        _model = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model!;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<string> GenerateEventsAsync(EventGenerationRequest request, CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = _model,
            temperature = 0.7,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new
                {
                    role = "user",
                    content = $"{request.Prompt}\nReturn JSON array with exactly {request.Count} items."
                }
            }
        };

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, "v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI error {response.StatusCode}: {body}");

        using JsonDocument doc = JsonDocument.Parse(body);
        string? content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("OpenAI returned empty content.");

        // response_format json_object wraps in {"events":[...]} so unwrap when needed
        if (content.TrimStart().StartsWith("{"))
        {
            using JsonDocument outer = JsonDocument.Parse(content);
            if (outer.RootElement.TryGetProperty("events", out JsonElement eventsElement))
                return eventsElement.GetRawText();
        }

        return content;
    }

    public string DumpRequestPayload(EventGenerationRequest request)
    {
        var payload = new
        {
            model = _model,
            temperature = 0.7,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new
                {
                    role = "user",
                    content = $"{request.Prompt}\nReturn JSON array with exactly {request.Count} items."
                }
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    static string ResolveApiKey(string? explicitKey, string envVar)
    {
        if (!string.IsNullOrWhiteSpace(explicitKey))
            return explicitKey;
        string? env = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(env))
            return env;
        throw new InvalidOperationException($"API key missing. Pass --apiKey or set {envVar}.");
    }
}

sealed class GoogleEventGenerator : IEventGenerationProvider
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly HttpClient _httpClient;

    public GoogleEventGenerator(string? apiKey, string? model)
    {
        _apiKey = ResolveApiKey(apiKey, "GOOGLE_API_KEY");
        _model = string.IsNullOrWhiteSpace(model) ? "gemini-1.5-flash" : model!;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };
    }

    public async Task<string> GenerateEventsAsync(EventGenerationRequest request, CancellationToken cancellationToken)
    {
        string path = $"v1beta/models/{_model}:generateContent?key={_apiKey}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = request.SystemPrompt + "\n" + request.Prompt +
                                      $"\nReturn JSON array with exactly {request.Count} items." }
                    }
                }
            ],
            generation_config = new
            {
                temperature = 0.75,
                response_mime_type = "application/json"
            }
        };

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Google API error {response.StatusCode}: {body}");

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement candidates = doc.RootElement.GetProperty("candidates");
        if (candidates.GetArrayLength() == 0)
            throw new InvalidOperationException("Google API returned no candidates.");

        string? text = candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Google API returned empty content.");

        return text;
    }

    public string DumpRequestPayload(EventGenerationRequest request)
    {
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = request.SystemPrompt + "\n" + request.Prompt +
                                      $"\nReturn JSON array with exactly {request.Count} items." }
                    }
                }
            ],
            generation_config = new
            {
                temperature = 0.75,
                response_mime_type = "application/json"
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    static string ResolveApiKey(string? explicitKey, string envVar)
    {
        if (!string.IsNullOrWhiteSpace(explicitKey))
            return explicitKey;
        string? env = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(env))
            return env;
        throw new InvalidOperationException($"API key missing. Pass --apiKey or set {envVar}.");
    }
}

#region Shared Data Contracts

sealed class GameEvent
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public EventScope Scope { get; set; } = EventScope.Local;
    public bool IsBadEvent { get; set; }
    public List<FeaturedPerson> FeaturedPeople { get; set; } = new();
    public List<EventOption> Options { get; set; } = new();
}

sealed class EventOption
{
    public string Text { get; set; } = string.Empty;
    public int SanityRequired { get; set; }
    public int MoneyRequired { get; set; }
    public int ArtifactsRequired { get; set; }
    public int SanityChange { get; set; }
    public int MoneyChange { get; set; }
    public int ArtifactsChange { get; set; }
    public int InfluenceChange { get; set; }
    public int StabilityChange { get; set; }
    public int DevelopmentChange { get; set; }
    public int MinInfluenceRequired { get; set; }
    public int MinDevelopmentRequired { get; set; }
    public List<RegionModifierDefinition> Modifiers { get; set; } = new();
}

sealed class RegionModifierDefinition
{
    public string Name { get; set; } = string.Empty;
    public float DurationSeconds { get; set; }
    public float TickIntervalSeconds { get; set; }
    public int InfluencePerTick { get; set; }
    public int StabilityPerTick { get; set; }
    public int DevelopmentPerTick { get; set; }
}

sealed class FeaturedPerson
{
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
}

enum EventScope
{
    Local,
    Global,
    Personal
}

#endregion
