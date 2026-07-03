using System;
using System.Collections.Generic;
using ZeroBloat.Services.Manifest;
using ZeroBloat.Tweaks.Modules;

namespace ZeroBloat.Tweaks
{
    /// <summary>
    /// Builds a working ITweak instance from a manifest TweakDefinition.
    ///
    /// Most tweaks are "generic" — their behavior is fully described by
    /// the manifest data (registry path + value, or service name) and get
    /// built via GenericRegistryTweak / GenericServiceTweak with zero
    /// tweak-specific code.
    ///
    /// A small number of tweaks need custom logic that doesn't fit the
    /// generic shape (e.g. Classic Context Menu deletes a key on revert
    /// instead of writing a value). Those keep a hardcoded class, and this
    /// factory routes to it by ID via the _specialCases map below. Adding
    /// a new special-case tweak means: write the class, register it here.
    /// Adding a new *generic* tweak means: just add a JSON entry — no code.
    /// </summary>
    public static class TweakFactory
    {
        private static readonly Dictionary<string, Func<TweakDefinition, ITweak>> _specialCases = new()
        {
            ["classic_context_menu"] = _ => new ClassicContextMenuTweak(),
            ["disable_recall"] = _ => new DisableRecallTweak(),
            ["disable_copilot"] = _ => new DisableCopilotTweak(),
            ["local_account_delink"] = _ => new LocalAccountDelinkTweak()
        };

        public static ITweak Build(TweakDefinition def)
        {
            if (_specialCases.TryGetValue(def.Id, out var factory))
                return factory(def);

            return def.Type switch
            {
                "registry" => new GenericRegistryTweak(def),
                "service" => new GenericServiceTweak(def),
                _ => throw new NotSupportedException(
                    $"Tweak '{def.Id}' has unrecognized type '{def.Type}'. " +
                    "Expected 'registry' or 'service', or it must be registered " +
                    "as a special case in TweakFactory.")
            };
        }

        /// <summary>
        /// Builds every tweak defined in the given manifest. This is what
        /// the UI will call at startup to populate the tweak list.
        /// </summary>
        public static List<ITweak> BuildAll(TweakManifest manifest)
        {
            var result = new List<ITweak>();
            foreach (var def in manifest.Tweaks)
            {
                try
                {
                    result.Add(Build(def));
                }
                catch (Exception ex)
                {
                    // A single malformed manifest entry shouldn't take down
                    // the whole app — skip it and keep going. This should
                    // be surfaced to the Activity Log once that exists.
                    System.Diagnostics.Debug.WriteLine(
                        $"[TweakFactory] Failed to build tweak '{def.Id}': {ex.Message}");
                }
            }
            return result;
        }
    }
}