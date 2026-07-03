using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using ZeroBloat.Services;

namespace ZeroBloat.Tweaks.Modules
{
    /// <summary>
    /// Disables Copilot's taskbar presence and background behavior. Uses
    /// two independent registry strategies: the Group Policy control (which
    /// governs the underlying feature) and the taskbar icon visibility key
    /// (which Explorer reads separately). Copilot also updates via the
    /// Microsoft Store independently of Windows Update, which is why the
    /// watchdog needs to check this one on every run (logon + weekly),
    /// not just after an Event ID 19 trigger — see the watchdog design
    /// notes when that gets built.
    ///
    /// Both registry paths below are publicly documented Copilot controls.
    /// Not yet verified against real hardware with Copilot present.
    /// </summary>
    public class DisableCopilotTweak : MultiStrategyTweakBase
    {
        public override string Id => "disable_copilot";
        public override string DisplayName => "Disable Copilot Entirely";
        public override string Description =>
            "Removes the Copilot taskbar icon and disables its background " +
            "service behavior via Group Policy.";
        public override TweakRiskTier RiskTier => TweakRiskTier.Standard;

        protected override int CurrentBuildNumber => SystemInfo.Instance.BuildNumber;

        public override IReadOnlyList<ITweakStrategy> Strategies => new List<ITweakStrategy>
        {
            // Primary: the documented Group Policy control, set in BOTH
            // hives — multiple current reports indicate HKLM alone is
            // often ignored; HKCU is required on some builds.
            new RegistryValueStrategy(
                tweakId: Id,
                strategyName: "PolicyKeyMachine",
                hive: RegistryHive.LocalMachine,
                subKeyPath: @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot",
                valueName: "TurnOffWindowsCopilot",
                enabledValue: 1,
                valueKind: RegistryValueKind.DWord,
                minBuildNumber: 22621
            ),
            new RegistryValueStrategy(
                tweakId: Id,
                strategyName: "PolicyKeyUser",
                hive: RegistryHive.CurrentUser,
                subKeyPath: @"Software\Policies\Microsoft\Windows\WindowsCopilot",
                valueName: "TurnOffWindowsCopilot",
                enabledValue: 1,
                valueKind: RegistryValueKind.DWord,
                minBuildNumber: 22621
            ),

            // Fallback: directly hide the taskbar icon (cosmetic only —
            // Microsoft's own guidance confirms Win+C can still launch
            // Copilot even with this set, so this alone is not sufficient).
            new RegistryValueStrategy(
                tweakId: Id,
                strategyName: "TaskbarIcon",
                hive: RegistryHive.CurrentUser,
                subKeyPath: @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                valueName: "ShowCopilotButton",
                enabledValue: 0,
                valueKind: RegistryValueKind.DWord,
                minBuildNumber: 22621
            ),

            // Most reliable in current real-world reports: remove the app
            // package directly, since the legacy policy key is flagged by
            // Microsoft for near-term deprecation and is inconsistently
            // honored across builds. Doesn't prevent silent reinstallation
            // on a future Windows Update — paired with the policy keys
            // above specifically to catch that case.
            new AppPackageRemoveStrategy(
                strategyName: "AppPackageRemoval",
                packageNamePattern: "Copilot",
                minBuildNumber: 22621
            )
        };

        public override TweakResult Apply()
        {
            // Override the base "stop at first success" behavior: for
            // Copilot specifically, the strategies are complementary
            // defense layers (policy + icon + app removal), not alternate
            // routes to the same effect. Applying only one and declaring
            // victory is what caused Copilot to appear "disabled" per the
            // registry while Win+C still launched it — the policy key
            // alone isn't sufficient in current real-world reports.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var applicable = Strategies.Where(s => s.IsApplicableToBuild(CurrentBuildNumber)).ToList();
            var log = new System.Collections.Generic.List<string>();
            bool anySuccess = false;

            foreach (var strategy in applicable)
            {
                var result = strategy.Apply();
                log.Add($"{strategy.StrategyName}: {(result.Success ? "OK" : "failed - " + result.Message)}");
                anySuccess |= result.Success;
            }

            sw.Stop();

            if (anySuccess)
                ShellRefreshHelper.RestartExplorer();

            return new TweakResult
            {
                Success = anySuccess,
                Duration = sw.Elapsed,
                Message = (anySuccess
                    ? $"Applied {DisplayName} — {log.Count(l => l.Contains("OK"))}/{applicable.Count} strategies succeeded."
                    : $"Failed to apply {DisplayName} — no strategies succeeded.") +
                    $"\nAttempt log: {string.Join(" | ", log)}" +
                    (anySuccess ? " (Explorer restarted.)" : "") +
                    "\n\nNote: Win+C may still launch Copilot even after this — Microsoft's own " +
                    "guidance confirms the keyboard shortcut can bypass taskbar/policy blocks on " +
                    "some builds. This is a known Windows limitation, not necessarily a failure here."
            };
        }

        public override TweakResult Verify()
        {
            var applicable = Strategies.Where(s => s.IsApplicableToBuild(CurrentBuildNumber)).ToList();
            var drifted = new System.Collections.Generic.List<string>();

            foreach (var strategy in applicable)
            {
                var result = strategy.Verify();
                if (!result.Success)
                    drifted.Add(strategy.StrategyName);
            }

            bool allGood = drifted.Count == 0;
            return new TweakResult
            {
                Success = allGood,
                Message = allGood
                    ? $"{DisplayName} verified — all {applicable.Count} strategies still enforced."
                    : $"{DisplayName} — {drifted.Count} of {applicable.Count} strategies drifted: {string.Join(", ", drifted)}."
            };
        }

        public override TweakResult Revert()
        {
            var result = base.Revert();
            if (result.Success)
            {
                ShellRefreshHelper.RestartExplorer();
                result.Message += " (Explorer restarted.)";
            }
            return result;
        }
    }
}