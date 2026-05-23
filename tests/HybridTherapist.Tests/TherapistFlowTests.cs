using FluentAssertions;
using HybridTherapist.Application.Flows;
using HybridTherapist.Application.Layers;
using HybridTherapist.Application.Options;
using HybridTherapist.Domain.Models;
using HybridTherapist.Infrastructure.Adapters;
using HybridTherapist.Infrastructure.State;
using HybridTherapist.Infrastructure.Tracing;
using HybridTherapist.Security.Gates;
using HybridTherapist.Security.Privacy;
using HybridTherapist.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HybridTherapist.Tests;

public sealed class TherapistFlowTests
{
    private static TherapistFlow CreateFlow(IOllamaAdapter ollama)
    {
        var opts = Options.Create(new TherapistOptions());
        var trace = new InMemoryTraceSink();
        var layers = new TherapistLayerService(ollama, opts, trace, NullLogger<TherapistLayerService>.Instance);
        var analyst = new AnalystLayer(ollama, opts, trace, NullLogger<AnalystLayer>.Instance);
        var supervisor = new SupervisorLayer(ollama, opts, trace, NullLogger<SupervisorLayer>.Instance);
        var memory = new TherapyMemoryService(ollama, opts, trace, NullLogger<TherapyMemoryService>.Instance);

        return new TherapistFlow(
            new CrisisGate(),
            new PrivacySanitizer(),
            new InMemoryTherapyStateRepository(),
            layers,
            analyst,
            supervisor,
            memory,
            opts,
            NullLogger<TherapistFlow>.Instance);
    }

    private static ChatCompletionRequest MakeRequest(string text) => new()
    {
        Model = "hybrid-therapist",
        Messages = [new ChatMessage { Role = "user", Content = text }],
    };

    // ── T1: All layers fail → BuildFallback with diagnostics ────────────────

    [Fact]
    public async Task ExecuteAsync_AllLayersFail_ReturnsBuildFallback()
    {
        var fake = new FakeOllamaAdapter(
            new LlmResponse { Ok = false, Error = "connection refused" });
        TherapistFlow flow = CreateFlow(fake);

        FlowExecutionResult result = await flow.ExecuteAsync(MakeRequest("cant sleep"));

        result.Fallback.Should().BeTrue();
        result.Content.Should().Contain("Przepraszam");
        result.Metadata.Should().ContainKey("failed_layer");
        result.Metadata["failed_layer"].Should().NotBeNull();
        result.Metadata.Should().ContainKey("error_reason");
        result.Metadata["error_reason"].Should().NotBeNull();
    }

    // ── T2: Crisis input short-circuits before any LLM call ────────────────

    [Fact]
    public async Task ExecuteAsync_CrisisInput_ReturnsHardStop()
    {
        var fake = new FakeOllamaAdapter(
            new LlmResponse { Ok = true, Text = "SHOULD NOT BE CALLED", ModelId = "fake" });
        TherapistFlow flow = CreateFlow(fake);

        FlowExecutionResult result = await flow.ExecuteAsync(MakeRequest("i want to kill myself"));

        result.CrisisDetected.Should().BeTrue();
        result.Fallback.Should().BeFalse();
        result.Content.Should().Contain("116 123");
        // CrisisGate fires before any LayerService call, so LLM text must never appear
        result.Content.Should().NotContain("SHOULD NOT BE CALLED");
    }

    // ── T3: L4 fails → fallback with correct layer diagnostics ─────────────

    [Fact]
    public async Task ExecuteAsync_L4Fails_ReturnsFallbackWithLayerDiagnostics()
    {
        var opts = Options.Create(new TherapistOptions());
        var perModel = new Dictionary<string, LlmResponse>
        {
            [opts.Value.Translator] = new() { Ok = true, Text = "I cannot sleep", ModelId = "fake-tr" },
            [opts.Value.Analyst] = new() { Ok = true,
                Text = "EMOTIONAL STATE: tired\nSEVERITY: low\nRISK INDICATORS: none\nCOGNITIVE PATTERNS: rumination\nEVIDENCE: user said they can't sleep",
                ModelId = "fake-an" },
            [opts.Value.Supervisor] = new() { Ok = true,
                Text = "APPROACH: CBT\nTECHNIQUE: sleep_hygiene\nKEY QUESTION: what keeps you awake?\nRISK NOTE: none",
                ModelId = "fake-su" },
            [opts.Value.Therapist] = new() { Ok = false, Error = "model not loaded", ModelId = "fake-th" },
            [opts.Value.Calibrator] = new() { Ok = true, Text = "calibrated output", ModelId = "fake-ca" },
        };
        var fake = new FakeOllamaAdapter(perModel);
        TherapistFlow flow = CreateFlow(fake);

        FlowExecutionResult result = await flow.ExecuteAsync(MakeRequest("cant sleep"));

        result.Fallback.Should().BeTrue();
        result.Content.Should().Contain("Przepraszam");
        result.Metadata["failed_layer"].Should().Be("L4_therapist");
        result.Metadata["error_reason"].Should().NotBeNull();
        result.Metadata["error_reason"]!.ToString()!.Should().Contain("model not loaded");
        // The bug: L1/L2/L3 did OK, only L4 failed
        result.Metadata.Should().ContainKey("trace_url");
    }

    // ── T4: Same input → same session ID (SHA256 determinism) ──────────────

    [Fact]
    public async Task ResolveSessionId_SameInput_SameOutput()
    {
        var fake = new FakeOllamaAdapter(
            new LlmResponse { Ok = false, Error = "not used" });
        TherapistFlow flow = CreateFlow(fake);

        var req1 = MakeRequest("jestem zmeczony");
        var req2 = MakeRequest("jestem zmeczony");
        FlowExecutionResult r1 = await flow.ExecuteAsync(req1);
        FlowExecutionResult r2 = await flow.ExecuteAsync(req2);

        string s1 = r1.Metadata["session_id"]?.ToString() ?? string.Empty;
        string s2 = r2.Metadata["session_id"]?.ToString() ?? string.Empty;

        s1.Should().Be(s2, "same first-user message must produce identical session_id");
        s1.Should().StartWith("sess_");
    }

    // ── T5: Different inputs → different session IDs ──────────────────────

    [Fact]
    public async Task ResolveSessionId_DifferentInputs_DifferentOutputs()
    {
        var fake = new FakeOllamaAdapter(
            new LlmResponse { Ok = false, Error = "not used" });
        TherapistFlow flow = CreateFlow(fake);

        FlowExecutionResult r1 = await flow.ExecuteAsync(MakeRequest("hello"));
        FlowExecutionResult r2 = await flow.ExecuteAsync(MakeRequest("world"));

        string s1 = r1.Metadata["session_id"]?.ToString() ?? string.Empty;
        string s2 = r2.Metadata["session_id"]?.ToString() ?? string.Empty;

        s1.Should().NotBe(s2, "different first-user messages must produce different session IDs");
    }
}
