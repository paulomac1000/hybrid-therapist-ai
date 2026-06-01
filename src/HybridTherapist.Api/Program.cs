using HybridTherapist.Api.Endpoints;
using HybridTherapist.Application.Flows;
using HybridTherapist.Application.Options;
using HybridTherapist.Domain.Interfaces;
using HybridTherapist.Infrastructure.Adapters;
using HybridTherapist.Infrastructure.State;
using HybridTherapist.Infrastructure.Tracing;
using HybridTherapist.Security.Gates;
using HybridTherapist.Security.Privacy;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// ── HTTP client — local Ollama only (zero cloud for Socrates pipeline) ──────
builder.Services.AddHttpClient("ollama", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
    c.Timeout = TimeSpan.FromMinutes(5);
});

// ── Options ──────────────────────────────────────────────────────────────────
// Stack.yaml is the source of truth (cortexa parity). Falls back to appsettings.json.
string? stackYamlPath = builder.Configuration["Models:StackYamlPath"]
    ?? builder.Configuration["ModelRegistry:StackYamlPath"];

if (!string.IsNullOrWhiteSpace(stackYamlPath) && File.Exists(stackYamlPath))
{
    StackConfig stack = StackConfig.Load(stackYamlPath);
    builder.Services.AddSingleton(stack);
    builder.Services.Configure<TherapistOptions>(opts =>
    {
        builder.Configuration.GetSection(TherapistOptions.Section).Bind(opts);
        opts.ApplyStackYaml(stack);
    });
}
else
{
    builder.Services.Configure<TherapistOptions>(
        builder.Configuration.GetSection(TherapistOptions.Section));
}

// ── Security (singleton — pattern caches are expensive to compile) ────────────
builder.Services.AddSingleton<CrisisGate>();
builder.Services.AddSingleton<PrivacySanitizer>();

// ── Infrastructure ────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ITherapyConversationStateRepository, InMemoryTherapyStateRepository>();
builder.Services.AddSingleton<ITraceSink, InMemoryTraceSink>();
builder.Services.AddSingleton<IOllamaAdapter, OllamaAdapter>();

// ── Application ───────────────────────────────────────────────────────────────
builder.Services.AddSingleton<TherapistLayerService>();
builder.Services.AddSingleton<HybridTherapist.Application.Layers.AnalystLayer>();
builder.Services.AddSingleton<HybridTherapist.Application.Layers.SupervisorLayer>();
builder.Services.AddSingleton<HybridTherapist.Application.Layers.TherapyMemoryService>();
builder.Services.AddScoped<TherapistFlow>();

var app = builder.Build();

// Enable request body buffering so we can read it multiple times
// (necessary for diagnostic logging in ChatEndpoints and LibreChat integration)
app.Use(async (ctx, next) =>
{
    ctx.Request.EnableBuffering();
    await next();
});

app.MapChatEndpoints();
app.MapTraceEndpoints();

app.Run();

// Exposed for WebApplicationFactory in integration tests
public partial class Program { }
