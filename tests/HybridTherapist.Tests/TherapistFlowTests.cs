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
            [opts.Value.Analyst] = new()
            {
                Ok = true,
                Text = "EMOTIONAL STATE: tired\nSEVERITY: low\nRISK INDICATORS: none\nCOGNITIVE PATTERNS: rumination\nEVIDENCE: user said they can't sleep",
                ModelId = "fake-an"
            },
            [opts.Value.Supervisor] = new()
            {
                Ok = true,
                Text = "APPROACH: CBT\nTECHNIQUE: sleep_hygiene\nKEY QUESTION: what keeps you awake?\nRISK NOTE: none",
                ModelId = "fake-su"
            },
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

    // ── T4: Severity escalation — anhedonia triggers EXPLORATION + concrete response

    [Fact]
    public async Task ExecuteAsync_AnhedoniaInput_EscalatesSeverityAndProvidesConcreteAdvice()
    {
        var perModel = new Dictionary<string, LlmResponse>
        {
            // L1 + L7: use same translator model — returns PL text for simplicity
            ["SpeakLeash/bielik-minitron-7b-v3.0-instruct:Q4_K_M"] = new LlmResponse { Ok = true, Text = "nic mnie już nie cieszy", ModelId = "translator" },
            ["hf.co/mradermacher/MentaLLaMA-chat-7B-GGUF:Q4_K_M"] = new LlmResponse
            {
                Ok = true,
                Text = "M|L=2|em=depressed|sv=high|ri=anhedonia,social_withdrawal|cp=hopelessness",
                ModelId = "analyst"
            },
            ["hf.co/RyanGichuru254/PsyLLM-8B-GGUF:Q4_K_M"] = new LlmResponse
            {
                Ok = true,
                Text = "M|L=3|ap=behavioral_activation|tk=schedule_one_small_activity|kq=What_One_Tiny_Thing_Could_You_Try_Today?|rn=none",
                ModelId = "supervisor"
            },
            ["hf.co/mradermacher/PsychoCounsel-Llama3-8B-GGUF:Q4_K_S"] = new LlmResponse
            {
                Ok = true,
                Text = "R|C=0.95|V=To musi być naprawdę trudne. Może spróbuj jednej małej rzeczy — wyjść na 5-minutowy spacer. Co o tym myślisz?",
                ModelId = "therapist"
            },
            ["hf.co/mradermacher/llama4-dolphin-8B-GGUF:Q4_K_S"] = new LlmResponse
            {
                Ok = true,
                Text = "To musi być naprawdę trudne. Może spróbuj jednej małej rzeczy.",
                ModelId = "calibrator"
            },
        };

        var fake = new FakeOllamaAdapter(perModel);
        TherapistFlow flow = CreateFlow(fake);

        FlowExecutionResult result = await flow.ExecuteAsync(
            MakeRequest("nic mnie nie cieszy, nie mam siły na nic"));

        result.Fallback.Should().BeFalse();
        result.Content.Should().NotBeNullOrWhiteSpace();
        result.Content.TrimStart().Should().NotStartWith("Rozumiem, że");
        result.Content.TrimStart().Should().NotStartWith("Widzę, że");
        result.Content.TrimStart().Should().NotStartWith("Słyszę, że");
        result.Metadata.Should().ContainKey("severity");
        result.Metadata["severity"].Should().Be("high",
            "anhedonia input ('nic mnie nie cieszy, nie mam siły na nic') must escalate severity to high");
        result.Metadata["fallback"].Should().Be(false);
    }

    // ── T5: QA enforcement — formulaic calibrator output blocks response ──────

    [Fact]
    public async Task ExecuteAsync_FormulaicCalibratorOutput_QaBlocksResponse()
    {
        var perModel = new Dictionary<string, LlmResponse>
        {
            ["SpeakLeash/bielik-minitron-7b-v3.0-instruct:Q4_K_M"] = new LlmResponse { Ok = true, Text = "I feel terrible", ModelId = "translator" },
            ["hf.co/mradermacher/MentaLLaMA-chat-7B-GGUF:Q4_K_M"] = new LlmResponse
            {
                Ok = true,
                Text = "M|L=2|em=anxiety|sv=moderate|ri=insomnia|cp=worry",
                ModelId = "analyst"
            },
            ["hf.co/RyanGichuru254/PsyLLM-8B-GGUF:Q4_K_M"] = new LlmResponse
            {
                Ok = true,
                Text = "M|L=3|ap=CBT|tk=thought_record|kq=What_evidence_supports_that?|rn=none",
                ModelId = "supervisor"
            },
            ["hf.co/mradermacher/PsychoCounsel-Llama3-8B-GGUF:Q4_K_S"] = new LlmResponse
            {
                Ok = true,
                Text = "R|C=0.9|V=That must be difficult. Try taking a short walk.",
                ModelId = "therapist"
            },
            // Calibrator returns formulaic opening → QA should block
            ["hf.co/mradermacher/llama4-dolphin-8B-GGUF:Q4_K_S"] = new LlmResponse
            {
                Ok = true,
                Text = "I understand that you feel terrible. How can I help you today?",
                ModelId = "calibrator"
            },
        };

        var fake = new FakeOllamaAdapter(perModel);
        TherapistFlow flow = CreateFlow(fake);

        FlowExecutionResult result = await flow.ExecuteAsync(
            MakeRequest("czuję się fatalnie"));

        // QA should have detected formulaic opening and returned fallback
        result.Fallback.Should().BeTrue("formulaic 'I understand' must trigger QA fallback");
        result.Metadata.Should().ContainKey("failed_layer");
    }

    // ── Session resolution ──────────────────────────────────────────────────

    [Fact]
    public void ResolveSessionId_WithUser_UsesUserHash_NotMessageHash()
    {
        var req1 = new ChatCompletionRequest
        {
            Model = "hybrid-therapist",
            Messages = [new ChatMessage { Role = "user", Content = "cześć" }],
            User = "test_user_123",
        };
        var req2 = new ChatCompletionRequest
        {
            Model = "hybrid-therapist",
            Messages = [new ChatMessage { Role = "user", Content = "zupełnie inna wiadomość" }],
            User = "test_user_123",
        };

        string session1 = TherapistFlow.ResolveSessionId(req1);
        string session2 = TherapistFlow.ResolveSessionId(req2);

        session1.Should().Be(session2, "same User must produce same session ID");
        session1.Should().StartWith("user_", "user-scoped sessions use 'user_' prefix");
    }

    [Fact]
    public void ResolveSessionId_WithoutUser_UsesMessageHash()
    {
        var req = new ChatCompletionRequest
        {
            Model = "hybrid-therapist",
            Messages = [new ChatMessage { Role = "user", Content = "cześć" }],
        };

        string session = TherapistFlow.ResolveSessionId(req);
        session.Should().StartWith("sess_", "legacy sessions use 'sess_' prefix");
    }

    [Fact]
    public void ResolveSessionId_DifferentUsers_GetDifferentSessions()
    {
        var req1 = new ChatCompletionRequest
        {
            Model = "hybrid-therapist",
            Messages = [new ChatMessage { Role = "user", Content = "cześć" }],
            User = "user_a",
        };
        var req2 = new ChatCompletionRequest
        {
            Model = "hybrid-therapist",
            Messages = [new ChatMessage { Role = "user", Content = "cześć" }],
            User = "user_b",
        };

        string session1 = TherapistFlow.ResolveSessionId(req1);
        string session2 = TherapistFlow.ResolveSessionId(req2);

        session1.Should().NotBe(session2, "different Users must get different sessions");
    }
}
