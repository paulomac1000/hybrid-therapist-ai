using FluentAssertions;
using HybridTherapist.Domain.Services;
using Xunit;

namespace HybridTherapist.Tests;

public sealed class QualityValidatorTests
{
    // ── English draft ────────────────────────────────────────────────────────

    [Fact]
    public void ValidateEnglishDraft_GoodResponse_Ok()
    {
        var v = QualityValidator.ValidateEnglishDraft(
            "I hear how exhausting that's been. What keeps your mind awake at night?",
            "I cannot sleep for three weeks");
        v.Ok.Should().BeTrue();
        v.Reason.Should().Be("ok");
    }

    [Theory]
    [InlineData("", "user")]
    [InlineData("   ", "user")]
    public void ValidateEnglishDraft_Empty_Fails(string draft, string user)
    {
        QualityValidator.ValidateEnglishDraft(draft, user).Ok.Should().BeFalse();
    }

    [Fact]
    public void ValidateEnglishDraft_TooShort_Fails()
    {
        var v = QualityValidator.ValidateEnglishDraft("OK.", "I cannot sleep");
        v.Ok.Should().BeFalse();
        v.Reason.Should().Be("too_short");
    }

    [Fact]
    public void ValidateEnglishDraft_Echo_Fails()
    {
        // Response is literally the user input
        var v = QualityValidator.ValidateEnglishDraft("I cannot sleep for three weeks", "I cannot sleep for three weeks");
        v.Ok.Should().BeFalse();
        v.Reason.Should().Be("echo_detected");
    }

    [Theory]
    [InlineData("Here is my answer: confidence_decimal is 0.9")]
    [InlineData("[ANALYST CONTEXT] should not leak to user")]
    [InlineData("Translate this YOUR_TRANSLATION text please")]
    [InlineData("Original user message (Polish): nie mogę zasnąć")]
    public void ValidateEnglishDraft_PromptLeakage_Fails(string draft)
    {
        var v = QualityValidator.ValidateEnglishDraft(draft, "user");
        v.Ok.Should().BeFalse();
        v.Reason.Should().Be("prompt_leakage");
    }

    // ── Polish output ────────────────────────────────────────────────────────

    [Fact]
    public void ValidatePolishOutput_GoodPolish_Ok()
    {
        var v = QualityValidator.ValidatePolishOutput(
            "Rozumiem, że masz trudności z zasypianiem. Co cię budzi w nocy?",
            "nie mogę zasnąć");
        v.Ok.Should().BeTrue();
    }

    [Fact]
    public void ValidatePolishOutput_EnglishMasqueradingAsPolish_Fails()
    {
        // Long output, no Polish diacritics → not actually Polish
        var v = QualityValidator.ValidatePolishOutput(
            "I understand you are having trouble sleeping. What keeps you up at night?",
            "nie moge zasnac");
        v.Ok.Should().BeFalse();
        v.Reason.Should().Be("not_polish");
    }

    [Fact]
    public void ValidatePolishOutput_PromptLeakage_Fails()
    {
        var v = QualityValidator.ValidatePolishOutput(
            "Translated Polish response: confidence_decimal jest 0.95",
            "nie mogę zasnąć");
        v.Ok.Should().BeFalse();
        v.Reason.Should().Be("prompt_leakage");
    }

    [Fact]
    public void ValidatePolishOutput_TooShort_Fails()
    {
        var v = QualityValidator.ValidatePolishOutput("Tak.", "nie mogę zasnąć");
        v.Ok.Should().BeFalse();
        v.Reason.Should().Be("too_short");
    }

    // ── Therapeutic quality checks ────────────────────────────────────────────

    [Fact]
    public void ValidateTherapeuticQuality_GoodResponse_Ok()
    {
        var v = QualityValidator.ValidateTherapeuticQuality(
            "To musi być trudne. Może spróbuj jednej małej rzeczy przed snem — odłożyć telefon 30 minut wcześniej.",
            "EXPLORATION", 4);
        v.Ok.Should().BeTrue();
    }

    [Fact]
    public void ValidateTherapeuticQuality_FormulaicOpening_Fails()
    {
        var v = QualityValidator.ValidateTherapeuticQuality(
            "Rozumiem, że czujesz się źle. Opowiedz więcej.",
            "INIT", 1);
        v.Ok.Should().BeFalse();
        v.Reason.Should().Be("formulaic_opening");
    }

    [Theory]
    [InlineData("Widzę, że to trudne. Co myślisz?", "formulaic_opening")]
    [InlineData("Słyszę, że jest Ci ciężko. Opowiedz.", "formulaic_opening")]
    public void ValidateTherapeuticQuality_OtherFormulaic_Fails(string text, string expectedReason)
    {
        var v = QualityValidator.ValidateTherapeuticQuality(text, "EXPLORATION", 2);
        v.Ok.Should().BeFalse();
        v.Reason.Should().Be(expectedReason);
    }

    [Fact]
    public void ValidateTherapeuticQuality_OnlyQuestionsAfter4Messages_Fails()
    {
        var v = QualityValidator.ValidateTherapeuticQuality(
            "To ważne co mówisz. Jak się z tym czujesz? Czy chcesz o tym porozmawiać?",
            "EXPLORATION", 5);
        v.Ok.Should().BeFalse();
        v.Reason.Should().Be("only_questions_after_4_messages");
    }

    [Fact]
    public void ValidateTherapeuticQuality_QuestionsBefore4Messages_Ok()
    {
        var v = QualityValidator.ValidateTherapeuticQuality(
            "Jak się z tym czujesz? Opowiedz mi więcej o tym co Cię trapi.",
            "INIT", 2);
        v.Ok.Should().BeTrue("only_questions check triggers at 4+ messages");
    }

    [Fact]
    public void ValidateTherapeuticQuality_ConcreteAdviceAfter4Messages_Ok()
    {
        var v = QualityValidator.ValidateTherapeuticQuality(
            "Warto spróbować techniki 5-4-3-2-1 — nazwij 5 rzeczy które widzisz. Co o tym myślisz?",
            "WORKING", 6);
        v.Ok.Should().BeTrue();
    }

    [Fact]
    public void ValidateTherapeuticQuality_EnglishReflectionWithQuestion_Ok()
    {
        // Reflection + question without advice keywords should NOT be rejected
        // (calibrator is instructed to avoid advice language after prompt hardening)
        var v = QualityValidator.ValidateTherapeuticQuality(
            "That sounds really heavy. It makes sense you'd feel exhausted carrying this alone. What first comes to mind when you imagine feeling better?",
            "EXPLORATION", 5);
        v.Ok.Should().BeTrue("reflection markers ('that sounds', 'it makes sense') indicate therapeutic substance even without advice");
    }

    [Fact]
    public void ValidateTherapeuticQuality_ThankYouAcknowledgment_Ok()
    {
        var v = QualityValidator.ValidateTherapeuticQuality(
            "Thank you for sharing that. I can imagine how draining that must feel. What helped you get through the worst moments?",
            "DIGGING", 5);
        v.Ok.Should().BeTrue("acknowledgment markers ('thank you for', 'I can imagine') indicate therapeutic substance");
    }

    [Fact]
    public void ValidateTherapeuticQuality_ValidationWithQuestion_Ok()
    {
        var v = QualityValidator.ValidateTherapeuticQuality(
            "That must be incredibly difficult. I can see why you'd feel overwhelmed. What does support look like for you right now?",
            "WORKING", 6);
        v.Ok.Should().BeTrue("validation markers ('that must be', 'I can see why') pass the check");
    }

    // ── English detectors ─────────────────────────────────────────────────────

    [Fact]
    public void ValidateTherapeuticQuality_EnglishFormulaicOpening_Fails()
    {
        var v = QualityValidator.ValidateTherapeuticQuality(
            "I understand this is difficult. Let me help.",
            "EXPLORATION", 3);
        v.Ok.Should().BeFalse();
        v.Reason.Should().Be("formulaic_opening");
    }

    [Theory]
    [InlineData("I see you're struggling. How can I help?")]
    [InlineData("I hear that you're in pain. Tell me more.")]
    [InlineData("It seems like you're going through a lot.")]
    public void ValidateTherapeuticQuality_EnglishFormulaicVariants_Fails(string text)
    {
        var v = QualityValidator.ValidateTherapeuticQuality(text, "INIT", 1);
        v.Ok.Should().BeFalse();
    }

    [Fact]
    public void ValidateTherapeuticQuality_EnglishAdvice_Detected()
    {
        var v = QualityValidator.ValidateTherapeuticQuality(
            "That must be hard. Try taking a few deep breaths when the anxiety peaks. How does that sound?",
            "WORKING", 5);
        v.Ok.Should().BeTrue("contains 'try' as advice");
    }

    [Fact]
    public void ValidateTherapeuticQuality_EnglishOnlyQuestions_Fails()
    {
        var v = QualityValidator.ValidateTherapeuticQuality(
            "I hear you. Can you tell me more? What does it feel like?",
            "EXPLORATION", 5);
        v.Ok.Should().BeFalse();
        v.Reason.Should().Be("only_questions_after_4_messages");
    }

    // ── Regression: "try" substring in non-advice words ───────────────────────

    [Theory]
    [InlineData("I hear you. The situation in your country must be difficult. What helps you cope?")]
    [InlineData("Can you tell me more? Your entry in the journal was meaningful.")]
    public void ValidateTherapeuticQuality_TrySubstringNotAdvice_FailsAfterMessages(string text)
    {
        var v = QualityValidator.ValidateTherapeuticQuality(text, "EXPLORATION", 5);
        v.Ok.Should().BeFalse("'try' substring in 'country'/'entry' must not count as advice");
        v.Reason.Should().Be("only_questions_after_4_messages");
    }

    [Fact]
    public void ValidateTherapeuticQuality_ReflectionWithPoetry_Ok()
    {
        // "That sounds like poetry" contains reflection marker but "try" in "poetry"
        // is NOT advice — the response should pass because reflection IS therapeutic substance
        var v = QualityValidator.ValidateTherapeuticQuality(
            "That sounds like poetry. How does it make you feel?",
            "EXPLORATION", 5);
        v.Ok.Should().BeTrue("reflection marker 'that sounds' overrides 'try' substring in 'poetry' — reflection is therapeutic");
    }
}
