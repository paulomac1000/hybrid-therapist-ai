using HandCodec.Models;

namespace HybridTherapist.Application.Options;

public sealed class TherapistOptions
{
    public const string Section = "Models";

    public string Translator { get; set; } = "SpeakLeash/bielik-minitron-7b-v3.0-instruct:Q4_K_M";
    public string Analyst { get; set; } = "hf.co/mradermacher/MentaLLaMA-chat-7B-GGUF:Q4_K_M";
    public string Supervisor { get; set; } = "hf.co/RyanGichuru254/PsyLLM-8B-GGUF:Q4_K_M";
    public string Therapist { get; set; } = "hf.co/mradermacher/PsychoCounsel-Llama3-8B-GGUF:Q4_K_S";
    public string Calibrator { get; set; } = "hf.co/mradermacher/llama4-dolphin-8B-GGUF:Q4_K_S";

    public AgentClass AgentClass { get; set; } = AgentClass.Assisted;
    public CompressionTier HandCompressionTier { get; set; } = CompressionTier.Balanced;
    public string TranslationFallbackPl { get; set; } = "Przepraszam, mam chwilowe trudności techniczne. Spróbuj ponownie za chwilę.";

    /// <summary>
    /// The wire format variant used for H.A.N.D. memos.
    /// Default is Compact (H.A.N.D. Compact keys e7, s9...).
    /// </summary>
    public HandWireVariant HandWireVariant { get; set; } = HandWireVariant.Compact;

    /// <summary>
    /// The number of checkpoint examples used for implicit priming (mimicry).
    /// Default is 3.
    /// </summary>
    public int ImplicitPrimingCheckpointCount { get; set; } = 3;
}

public enum HandWireVariant
{
    Compact,   // H.A.N.D. Compact keys (e7, s9, p3, k2...) — production default
    Semantic,  // H.A.N.D. Semantic keys (em, sv, ap, tk...)  — benchmark comparison
    Plaintext, // Natural-language paragraphs — benchmark comparison
    Json,      // JSON objects — benchmark comparison
}
