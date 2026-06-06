using FluentAssertions;
using Xunit;

namespace HybridTherapist.Tests;

/// <summary>
/// Pillar 2 — Implicit Priming (Stateless Negotiation Cache):
/// "How do you make a model write H.A.N.D. without ever telling it about the format?"
/// You don't. You show it.
///
/// System prompts describe the TASK in natural language (prose).
/// They MUST NOT contain wire-format instructions (no "Respond EXACTLY as M|",
/// no "Dictionary:", no "Output ONLY one line starting with M|").
///
/// The model learns the wire format exclusively from conversation-history
/// checkpoints (TherapyAnalystPing, TherapySupervisorPing, SystemPing).
/// </summary>
public sealed class ImplicitPrimingTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HybridTherapist.sln")))
                dir = dir.Parent;
            return dir?.FullName ?? AppContext.BaseDirectory;
        }
    }

    private static string SrcPath(string relative) =>
        Path.Combine(RepoRoot, relative);

    // ── Pillar 2 invariants: NO wire format instruction in system prompts ─────

    [Fact]
    public void Analyst_Prompt_HasNoWireFormatInstruction()
    {
        string source = File.ReadAllText(
            SrcPath("src/HybridTherapist.Application/Layers/AnalystLayer.cs"));

        source.Should().NotContain("Respond EXACTLY as a single M|",
            "System prompt must NEVER tell the model about wire format");
        source.Should().NotContain("Output ONLY one line starting with M|",
            "System prompt must NEVER instruct model to output M|");
        source.Should().NotContain("Dictionary:",
            "System prompt must NEVER list wire format dictionary");
        source.Should().NotContain("M|L=2|em=anxious|sv=moderate",
            "System prompt must NEVER contain wire format examples");
        source.Should().NotContain("M|L=2|e7=anxious|s9=moderate",
            "System prompt must NEVER contain wire format examples (codec G)");

        // Allowed: prose task description
        source.Should().Contain("clinical mental health analyst",
            "System prompt should describe the task in prose");
    }

    [Fact]
    public void Supervisor_Prompt_HasNoWireFormatInstruction()
    {
        string source = File.ReadAllText(
            SrcPath("src/HybridTherapist.Application/Layers/SupervisorLayer.cs"));

        source.Should().NotContain("Respond EXACTLY as a single M|");
        source.Should().NotContain("Output ONLY one line starting with M|");
        source.Should().NotContain("Dictionary:");
        source.Should().NotContain("M|L=3|p3=CBT");

        source.Should().Contain("clinical supervisor");
    }

    [Fact]
    public void Therapist_Prompts_HaveNoWireFormatInstruction()
    {
        string source = File.ReadAllText(
            SrcPath("src/HybridTherapist.Application/Flows/TherapistLayerService.cs"));

        // L4: no wire instruction OR key legend
        source.Should().NotContain("ABSOLUTELY FORBIDDEN OPENINGS");
        source.Should().NotContain("Analyst memo keys:");
        source.Should().NotContain("Supervisor memo keys:");
        source.Should().NotContain("em=emotional state");

        // L4 Pure Implicit: zero format hints in system prompt
        source.Should().NotContain("Use the information below");
        source.Should().NotContain("structured clinical context");
        source.Should().NotContain("Read the M| messages");
        source.Should().NotContain("memo keys");

        // L1/L7 translators: no wire instruction
        source.Should().NotContain("Respond EXACTLY as a single M|");
        source.Should().NotContain("Output ONLY one line starting with M|");

        // Allowed: prose task descriptions
        source.Should().Contain("You are a translator working");
        source.Should().Contain("You are an empathetic therapist.");
        source.Should().Contain("You are a therapeutic response editor.");
        source.Should().Contain("You are a Polish translator.");
    }

    // ── Checkpoint structure ─────────────────────────────────────────────────

    [Fact]
    public void TherapyAnalystPing_HasAtLeastTwoExamples()
    {
        var ping = HybridTherapist.Application.Hand.HandCheckpointLibrary.TherapyAnalystPing;
        ping.Exchanges.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void TherapySupervisorPing_HasAtLeastTwoExamples()
    {
        var ping = HybridTherapist.Application.Hand.HandCheckpointLibrary.TherapySupervisorPing;
        ping.Exchanges.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void NewBenchmarkPings_HaveAtLeastTwoExamples()
    {
        var lib = typeof(HybridTherapist.Application.Hand.HandCheckpointLibrary);
        var props = lib.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(HandRuntime.HandCheckpoint));

        foreach (var prop in props)
        {
            if (prop.Name == "SystemPing" || prop.Name == "MemoPing")
                continue;

            var ping = (HandRuntime.HandCheckpoint)prop.GetValue(null)!;
            ping.Exchanges.Should().HaveCountGreaterThan(1, $"Checkpoint {prop.Name} must have at least two priming examples");
        }
    }

    [Fact]
    public void TherapySupervisorPing_AlignedWithFallbackApproach()
    {
        var ping = HybridTherapist.Application.Hand.HandCheckpointLibrary.TherapySupervisorPing;
        ping.Exchanges[0].AssistantWire.Should().Contain("p3=behavioral_activation");
    }

    [Fact]
    public void AllCheckpointExchanges_AreNonDomain()
    {
        var lib = typeof(HybridTherapist.Application.Hand.HandCheckpointLibrary);
        var props = lib.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(HandRuntime.HandCheckpoint));

        foreach (var prop in props)
        {
            var ping = (HandRuntime.HandCheckpoint)prop.GetValue(null)!;
            foreach (var ex in ping.Exchanges)
            {
                ex.UserText.Should().Be("[SYSTEM_PROTOCOL_PING]", $"Checkpoint {prop.Name} exchanges must use non-domain system protocol ping");
                string[] forbiddenTherapyTerms = ["anxiety", "depression", "therapeutic", "clinical", "patient", "crisis"];
                ex.AssistantWire.Should().NotContainAny(forbiddenTherapyTerms,
                    $"Checkpoint {prop.Name} assistant wire must contain no therapeutic terms");
            }
        }
    }
}
