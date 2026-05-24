using FluentAssertions;
using Xunit;

namespace HybridTherapist.Tests;

/// <summary>
/// Pillar 2 — Implicit Priming (Stateless Negotiation Cache):
/// "How do you make a model write H.A.N.D. without ever telling it about the format?"
/// You don't. You show it.
///
/// These tests enforce the invariant that NO system prompt contains
/// wire-format instructions. The model learns the format exclusively
/// from conversation-history checkpoints, never from explicit commands.
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

    // ── Pillar 2 invariants ──────────────────────────────────────────────────

    [Fact]
    public void Analyst_SystemPrompt_MustNotContainWireFormatInstruction()
    {
        string analystSource = File.ReadAllText(
            SrcPath("src/HybridTherapist.Application/Layers/AnalystLayer.cs"));

        // Banned: explicit wire format instruction
        analystSource.Should().NotContain("Respond EXACTLY as a single M|",
            "System prompt must NEVER tell the model about wire format");
        analystSource.Should().NotContain("CRITICAL: Output ONLY one line starting with M|",
            "System prompt must NEVER instruct model to output M|");
        analystSource.Should().NotContain("\"Dictionary:\"",
            "System prompt must NEVER list wire format dictionary");

        // Allowed: task description only
        analystSource.Should().Contain("clinical mental health analyst",
            "System prompt should describe the task, not the format");
    }

    [Fact]
    public void Supervisor_SystemPrompt_MustNotContainWireFormatInstruction()
    {
        string supervisorSource = File.ReadAllText(
            SrcPath("src/HybridTherapist.Application/Layers/SupervisorLayer.cs"));

        supervisorSource.Should().NotContain("Respond EXACTLY as a single M|",
            "System prompt must NEVER tell the model about wire format");
        supervisorSource.Should().NotContain("CRITICAL: Output ONLY one line starting with M|",
            "System prompt must NEVER instruct model to output M|");
        supervisorSource.Should().NotContain("\"Dictionary:\"",
            "System prompt must NEVER list wire format dictionary");

        supervisorSource.Should().Contain("clinical supervisor",
            "System prompt should describe the task, not the format");
    }

    [Fact]
    public void Therapist_SystemPrompt_MustNotContainWireFormatInstruction()
    {
        string therapistSource = File.ReadAllText(
            SrcPath("src/HybridTherapist.Application/Flows/TherapistLayerService.cs"));

        therapistSource.Should().NotContain("FORBIDDEN openings",
            "L4 prompt must not contain style instructions — that goes in checkpoints");
        therapistSource.Should().NotContain("PREFERRED alternative",
            "Style guidance must not leak into system prompt");
        therapistSource.Should().NotContain("NEVER start with",
            "Formulaic opening rules belong in checkpoints, not system prompts");

        // Allowed: functional guidance
        therapistSource.Should().Contain("You are an empathetic therapist",
            "System prompt should describe the task");
    }

    // ── Checkpoint structure ─────────────────────────────────────────────────

    [Fact]
    public void TherapyAnalystPing_HasAtLeastTwoExamples()
    {
        var ping = HybridTherapist.Application.Hand.HandCheckpointLibrary.TherapyAnalystPing;
        ping.Exchanges.Should().HaveCountGreaterThan(1,
            "At least 2 diverse examples prevent model from copying a single pattern verbatim");
    }

    [Fact]
    public void TherapySupervisorPing_HasAtLeastTwoExamples()
    {
        var ping = HybridTherapist.Application.Hand.HandCheckpointLibrary.TherapySupervisorPing;
        ping.Exchanges.Should().HaveCountGreaterThan(1,
            "At least 2 diverse examples prevent model from copying a single pattern verbatim");
    }

    [Fact]
    public void TherapySupervisorPing_AlignedWithFallbackApproach()
    {
        var ping = HybridTherapist.Application.Hand.HandCheckpointLibrary.TherapySupervisorPing;
        // The first example should match the behavioral_activation fallback
        ping.Exchanges[0].AssistantWire.Should().Contain("ap=behavioral_activation",
            "First checkpoint example must align with the fallback approach so the model " +
            "never learns a pattern that contradicts the safety net");
    }

    [Fact]
    public void AllCheckpointExchanges_AreNonDomain()
    {
        // Both pings use [SYSTEM_PROTOCOL_PING] as user text — domain-neutral
        var analystPing = HybridTherapist.Application.Hand.HandCheckpointLibrary.TherapyAnalystPing;
        var supervisorPing = HybridTherapist.Application.Hand.HandCheckpointLibrary.TherapySupervisorPing;

        foreach (var ex in analystPing.Exchanges)
            ex.UserText.Should().Be("[SYSTEM_PROTOCOL_PING]",
                "User text must be non-domain to avoid context pollution");

        foreach (var ex in supervisorPing.Exchanges)
            ex.UserText.Should().Be("[SYSTEM_PROTOCOL_PING]",
                "User text must be non-domain to avoid context pollution");
    }
}
