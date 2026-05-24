namespace HybridTherapist.Domain.Services;

/// <summary>
/// Simple session phase machine. Phases: INIT → EXPLORATION → DIGGING → WORKING → CLOSING.
/// </summary>
public static class SessionPhase
{
    public static string Evaluate(string currentPhase, int messageCount, string severity = "low")
    {
        bool high = string.Equals(severity, "high", StringComparison.OrdinalIgnoreCase)
            || string.Equals(severity, "crisis", StringComparison.OrdinalIgnoreCase);
        bool moderate = string.Equals(severity, "moderate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(severity, "medium", StringComparison.OrdinalIgnoreCase);

        return currentPhase switch
        {
            "INIT" when high && messageCount >= 1 => "EXPLORATION",
            "INIT" when moderate && messageCount >= 1 => "EXPLORATION",
            "INIT" when messageCount >= 2 => "EXPLORATION",
            "EXPLORATION" when (high || moderate) && messageCount >= 4 => "DIGGING",
            "EXPLORATION" when messageCount >= 6 => "DIGGING",
            "DIGGING" when high && messageCount >= 8 => "WORKING",
            "DIGGING" when messageCount >= 12 => "WORKING",
            "WORKING" when messageCount >= 20 => "CLOSING",
            _ => currentPhase,
        };
    }

    public static string GetPhaseSystemPrompt(string phase) => phase switch
    {
        "INIT" =>
            "First contact. Welcome warmly and make the person feel safe. " +
            "Do NOT give clinical diagnoses or deep interpretations. " +
            "You MAY offer one gentle, practical suggestion if the person explicitly asks for help " +
            "(e.g. 'what can I do?', 'how to cope?'). " +
            "If the person asks for advice, give a concrete, small, actionable step. " +
            "Otherwise, listen and ask one open question.",

        "EXPLORATION" =>
            "The person is sharing more context. Explore their situation with curiosity and empathy. " +
            "Reflect what you hear and gently probe for more depth. Do not rush to solutions.",

        "DIGGING" =>
            "You're now helping the person go deeper. Identify patterns, unspoken emotions, " +
            "and underlying needs. Use reflective listening and Socratic questions.",

        "WORKING" =>
            "The person is ready to work on their issue. Help them identify options, " +
            "explore their own resources, and co-create small steps. No prescriptions.",

        "CLOSING" =>
            "The session is winding down. Help consolidate insights, express appreciation " +
            "for their openness, and gently close with an invitation to continue if needed.",

        _ =>
            "Listen with empathy and ask one clarifying question.",
    };

    public static string GetCalibratorPhaseGuidance(string phase) => phase switch
    {
        "INIT" =>
            "INIT phase: prefer warmth and safety. Longer responses OK (up to 250 words). " +
            "One gentle open question. Avoid clinical terminology. " +
            "NEVER open with formulaic phrases like 'I understand that' or 'It seems that'.",

        "EXPLORATION" =>
            "EXPLORATION phase: light touch. Ask open questions that invite elaboration. " +
            "Do NOT interpret — reflect only. " +
            "NEVER open with formulaic phrases. Vary your opening style.",

        "DIGGING" =>
            "DIGGING phase: precision matters. Follow up on specifics. " +
            "One focused question. Challenge gently if appropriate. " +
            "NEVER open with formulaic phrases. Use varied, natural openings.",

        "WORKING" =>
            "WORKING phase: action-oriented. Suggest concrete next steps. " +
            "Shorter responses (under 150 words). Practical tone. " +
            "NEVER open with formulaic phrases. Be direct and warm.",

        "CLOSING" =>
            "CLOSING phase: summary tone. Look forward, not backward. " +
            "No new deep topics. Gentle wrap-up. " +
            "NEVER open with formulaic phrases. Express appreciation for the conversation.",

        _ =>
            "NEVER open with formulaic phrases like 'I understand that' or 'It seems that'. " +
            "Vary the opening. End with one open-ended question.",
    };
}
