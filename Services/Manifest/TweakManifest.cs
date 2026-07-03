using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ZeroBloat.Services.Manifest
{
    /// <summary>
    /// Root object for tweaks.json. manifestVersion is checked against a
    /// minimum-supported version by the engine at load time — bump this
    /// whenever the schema shape changes in a way old app versions can't parse.
    /// </summary>
    public class TweakManifest
    {
        [JsonPropertyName("manifestVersion")]
        public string ManifestVersion { get; set; } = "0.0.0";

        [JsonPropertyName("generatedAt")]
        public string GeneratedAt { get; set; } = string.Empty;

        [JsonPropertyName("tweaks")]
        public List<TweakDefinition> Tweaks { get; set; } = new();
    }

    /// <summary>
    /// One entry in tweaks.json. Not every field applies to every "type" —
    /// registry-type tweaks use Hive/SubKeyPath/ValueName/etc, service-type
    /// tweaks use ServiceName/StartValue*. TweakFactory picks the right
    /// fields based on Type.
    /// </summary>
    public class TweakDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("riskTier")]
        public string RiskTier { get; set; } = "Standard"; // Safe | Standard | Gaming

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty; // "registry" | "service"

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty; // e.g. "setValue", "deleteKeyOnRevert", "stopAndDisable"

        // --- registry-type fields ---
        [JsonPropertyName("hive")]
        public string? Hive { get; set; }

        [JsonPropertyName("subKeyPath")]
        public string? SubKeyPath { get; set; }

        [JsonPropertyName("valueName")]
        public string? ValueName { get; set; }

        [JsonPropertyName("valueKind")]
        public string? ValueKind { get; set; }

        [JsonPropertyName("enabledValue")]
        public object? EnabledValue { get; set; }

        [JsonPropertyName("defaultValue")]
        public object? DefaultValue { get; set; }

        // --- service-type fields ---
        [JsonPropertyName("serviceName")]
        public string? ServiceName { get; set; }

        [JsonPropertyName("startValueDisabled")]
        public int? StartValueDisabled { get; set; }

        [JsonPropertyName("startValueDefault")]
        public int? StartValueDefault { get; set; }

        // --- compatibility ---
        [JsonPropertyName("compatibility")]
        public TweakCompatibilityRule? Compatibility { get; set; }
    }

    public class TweakCompatibilityRule
    {
        [JsonPropertyName("minBuildNumber")]
        public int MinBuildNumber { get; set; }

        [JsonPropertyName("maxBuildNumber")]
        public int? MaxBuildNumber { get; set; }

        [JsonPropertyName("editions")]
        public List<string> Editions { get; set; } = new();

        [JsonPropertyName("unsupportedBelow")]
        public string? UnsupportedBelow { get; set; }
    }
}