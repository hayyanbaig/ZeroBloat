using System;
using System.Collections.Generic;

namespace ZeroBloat.Tweaks
{
    /// <summary>
    /// Result of applying, verifying, or reverting a tweak. Carries enough
    /// detail to populate both the Dry-Run Diff Preview and the Activity Log
    /// without the UI needing to know how any individual tweak works.
    /// </summary>
    public class TweakResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        // Raw before/after values, shown verbatim in the diff preview and log.
        // e.g. Path: HKLM\SOFTWARE\...\Key   OldValue: "0"   NewValue: "1"
        public string TargetPath { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }

        public TimeSpan Duration { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Compatibility status shown as the live badge next to each tweak in
    /// the UI, driven by SystemInfo at runtime.
    /// </summary>
    public enum TweakCompatibility
    {
        Supported,
        Untested,
        Unsupported
    }

    public enum TweakRiskTier
    {
        Safe,       // Performance tab, no security trade-offs
        Standard,   // Anti-AI / Privacy — reversible, low risk
        Gaming      // Gated behind explicit confirmation (e.g. VBS disable)
    }

    /// <summary>
    /// Every tweak in ZeroBloat implements this interface. The engine,
    /// diff preview, undo system, and watchdog all operate against this
    /// contract rather than knowing anything about individual tweaks —
    /// this is what lets one tweak's Windows-Update breakage get fixed
    /// without touching the surrounding infrastructure.
    /// </summary>
    public interface ITweak
    {
        /// <summary>Unique, stable ID — used as the key in tweaks.json and the undo log. Never change this once shipped.</summary>
        string Id { get; }

        string DisplayName { get; }
        string Description { get; }
        TweakRiskTier RiskTier { get; }

        /// <summary>
        /// Reads the current system state and returns what WOULD change,
        /// without applying anything. Powers the Dry-Run Diff Preview.
        /// </summary>
        TweakResult PreviewChange();

        /// <summary>
        /// Applies the tweak. Implementations must record the pre-change
        /// value themselves (or return it in the result) so the undo
        /// system can persist it via DPAPI before this returns.
        /// </summary>
        TweakResult Apply();

        /// <summary>
        /// Reads the current state back and confirms it matches what
        /// Apply() was supposed to produce. Used by the watchdog to detect
        /// drift and by Apply() itself to confirm success rather than
        /// assuming it.
        /// </summary>
        TweakResult Verify();

        /// <summary>
        /// Reverts this specific tweak using the stored pre-change value.
        /// Must work independently of System Restore.
        /// </summary>
        TweakResult Revert();

        /// <summary>
        /// Checked against SystemInfo at UI render time to produce the
        /// live capability badge (✅ / ⚠️ / ❌).
        /// </summary>
        TweakCompatibility CheckCompatibility();
    }

    /// <summary>
    /// Optional interface for tweaks that need to try multiple strategies
    /// in sequence (e.g. Recall disable, Local Account De-Link) because a
    /// single Windows Update can close one route while leaving others intact.
    /// </summary>
    public interface IMultiStrategyTweak : ITweak
    {
        IReadOnlyList<ITweakStrategy> Strategies { get; }
    }

    public interface ITweakStrategy
    {
        string StrategyName { get; }
        bool IsApplicableToBuild(int buildNumber);
        TweakResult Apply();
        TweakResult Verify();
        TweakResult Revert();
    }
}