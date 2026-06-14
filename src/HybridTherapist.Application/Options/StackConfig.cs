using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HybridTherapist.Application.Options;

/// <summary>
/// Minimal stack.yaml shape — mirrors cortexa <c>config/stack.yaml</c>.
/// Only the <c>models:</c> section is consumed; vram and hardware tiers are ignored here.
/// </summary>
public sealed class StackConfig
{
    public string Version { get; set; } = "1.0";
    public Dictionary<string, ModelConfig> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public sealed class ModelConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Provider { get; set; } = "ollama";

        [YamlMember(Alias = "type", ApplyNamingConventions = false)]
        public string? Type { get; set; }

        public string Role { get; set; } = string.Empty;
        public double? VramBudgetGb { get; set; }
        public string? InferenceDevice { get; set; }
        public int? KeepAliveSeconds { get; set; }
    }

    public static StackConfig Load(string yamlPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yamlPath);
        if (!File.Exists(yamlPath))
            throw new FileNotFoundException($"stack.yaml not found at {yamlPath}", yamlPath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        using StreamReader reader = File.OpenText(yamlPath);
        return deserializer.Deserialize<StackConfig>(reader) ?? new StackConfig();
    }

    /// <summary>
    /// Resolve a model name by role. Lookup precedence:
    /// 1. Direct key match (case-insensitive)
    /// 2. <c>role</c> field match across all entries
    /// Throws when neither resolves.
    /// </summary>
    public string ResolveModelName(string roleOrKey)
    {
        if (Models.TryGetValue(roleOrKey, out ModelConfig? cfg) && !string.IsNullOrEmpty(cfg.Name))
            return cfg.Name;

        string? match = Models.Select(kvp => kvp)
            .Where(kvp => string.Equals(kvp.Value.Role, roleOrKey, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(kvp.Value.Name))
            .Select(kvp => kvp.Value.Name)
            .FirstOrDefault();
        if (match is not null) return match;

        throw new KeyNotFoundException(
            $"Model '{roleOrKey}' not found in stack.yaml. Known keys: [{string.Join(", ", Models.Keys)}]");
    }

    /// <summary>
    /// Resolve a model name, only accepting entries whose provider matches.
    /// The stack.yaml format uses the same key (e.g. <c>translator</c>) for both
    /// cloud and local variants in different deployments — this lets the caller
    /// require an Ollama-runnable entry for L1-L7 layers, and an OpenRouter entry
    /// for the cloud fallback slot.
    /// </summary>
    public string ResolveModelName(string roleOrKey, string requiredProvider)
    {
        if (Models.TryGetValue(roleOrKey, out ModelConfig? cfg)
            && !string.IsNullOrEmpty(cfg.Name)
            && ProviderMatches(cfg, requiredProvider))
        {
            return cfg.Name;
        }

        string? match = Models.Select(kvp => kvp)
            .Where(kvp => string.Equals(kvp.Value.Role, roleOrKey, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(kvp.Value.Name)
                && ProviderMatches(kvp.Value, requiredProvider))
            .Select(kvp => kvp.Value.Name)
            .FirstOrDefault();
        if (match is not null) return match;

        throw new KeyNotFoundException(
            $"Model '{roleOrKey}' (provider={requiredProvider}) not found in stack.yaml.");
    }

    private static bool ProviderMatches(ModelConfig cfg, string requiredProvider)
    {
        // Everything defaults to ollama; cloud is not supported.
        string actual = !string.IsNullOrEmpty(cfg.Provider)
            ? cfg.Provider
            : "ollama";
        return string.Equals(actual, requiredProvider, StringComparison.OrdinalIgnoreCase);
    }
}
