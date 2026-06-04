using System.Text.Json;
using System.Text.Json.Serialization;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace HybridTherapist.Integration;

/// <summary>
/// Stand-in for a live Ollama. Loads a cassette JSON file and serves
/// <c>/api/chat</c> responses by matching the request body against the
/// cassette's <c>request_match</c> rules. Used to run the Socrates
/// pipeline deterministically in CI without GPU or network.
/// </summary>
public sealed class CassetteOllamaServer : IAsyncDisposable
{
    private readonly WireMockServer _server;
    private readonly Cassette _cassette;

    private CassetteOllamaServer(WireMockServer server, Cassette cassette)
    {
        _server = server;
        _cassette = cassette;
    }

    public string BaseUrl => _server.Urls[0];
    public WireMockServer Server => _server;

    public static async Task<CassetteOllamaServer> StartAsync(string cassettePath)
    {
        await using FileStream fs = File.OpenRead(cassettePath);
        Cassette? cassette = await JsonSerializer.DeserializeAsync<Cassette>(fs, JsonOpts)
            ?? throw new InvalidDataException($"Cassette is empty: {cassettePath}");

        WireMockServer server = WireMockServer.Start();

        // /api/tags — list of pulled models. Cassettes can reference any model name; just say they're all present.
        server.Given(Request.Create().WithPath("/api/tags").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
              {
                  models = cassette.Interactions
                      .Select(i => new { name = i.Response.Model })
                      .DistinctBy(m => m.name)
                      .ToArray(),
              }));

        // /api/chat — match on model + user content
        server.Given(Request.Create().WithPath("/api/chat").UsingPost())
              .RespondWith(Response.Create().WithCallback(req =>
              {
                  Interaction? hit = MatchInteraction(cassette, req.Body ?? string.Empty);
                  if (hit is null)
                  {
                      return new WireMock.ResponseMessage
                      {
                          StatusCode = 404,
                          BodyData = new WireMock.Util.BodyData
                          {
                              DetectedBodyType = WireMock.Types.BodyType.Json,
                              BodyAsJson = new { error = "no cassette interaction matched", body_preview = req.Body?[..Math.Min(200, req.Body.Length)] },
                          },
                      };
                  }

                  return new WireMock.ResponseMessage
                  {
                      StatusCode = 200,
                      BodyData = new WireMock.Util.BodyData
                      {
                          DetectedBodyType = WireMock.Types.BodyType.Json,
                          BodyAsJson = new
                          {
                              model = hit.Response.Model,
                              message = new { role = hit.Response.Message.Role, content = hit.Response.Message.Content },
                              done = true,
                          },
                      },
                  };
              }));

        return new CassetteOllamaServer(server, cassette);
    }

    private static Interaction? MatchInteraction(Cassette cassette, string requestBody)
    {
        if (string.IsNullOrEmpty(requestBody)) return null;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(requestBody);
            JsonElement root = doc.RootElement;
            string modelId = root.TryGetProperty("model", out JsonElement m) ? m.GetString() ?? string.Empty : string.Empty;

            string allUserContent = string.Empty;
            if (root.TryGetProperty("messages", out JsonElement msgs) && msgs.ValueKind == JsonValueKind.Array)
            {
                var contents = msgs.EnumerateArray()
                    .Select(el => el.TryGetProperty("content", out JsonElement c) ? c.GetString() ?? string.Empty : string.Empty);
                allUserContent = string.Join("\n", contents);
            }

            foreach (Interaction i in cassette.Interactions)
            {
                bool modelOk = string.IsNullOrEmpty(i.RequestMatch.ModelContains)
                    || modelId.Contains(i.RequestMatch.ModelContains, StringComparison.OrdinalIgnoreCase);
                bool contentOk = string.IsNullOrEmpty(i.RequestMatch.UserContentContains)
                    || allUserContent.Contains(i.RequestMatch.UserContentContains, StringComparison.OrdinalIgnoreCase);

                if (modelOk && contentOk) return i;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        _server.Stop();
        _server.Dispose();
        await Task.CompletedTask;
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public sealed record Cassette(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("interactions")] IReadOnlyList<Interaction> Interactions);

    public sealed record Interaction(
        [property: JsonPropertyName("layer")] string Layer,
        [property: JsonPropertyName("request_match")] RequestMatch RequestMatch,
        [property: JsonPropertyName("response")] CassetteResponse Response);

    public sealed record RequestMatch(
        [property: JsonPropertyName("model_contains")] string? ModelContains,
        [property: JsonPropertyName("user_content_contains")] string? UserContentContains);

    public sealed record CassetteResponse(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("message")] CassetteMessage Message,
        [property: JsonPropertyName("done")] bool Done);

    public sealed record CassetteMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);
}
