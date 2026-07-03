using System.Collections.Generic;
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
            // Primary: the documented Group Policy control.
            new RegistryValueStrategy(
                tweakId: Id,
                strategyName: "PolicyKey",
                hive: RegistryHive.LocalMachine,
                subKeyPath: @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot",
                valueName: "TurnOffWindowsCopilot",
                enabledValue: 1,
                valueKind: RegistryValueKind.DWord,
                minBuildNumber: 22621
            ),

            // Fallback: directly hide the taskbar icon, in case the policy
            // key alone doesn't remove it from the taskbar on a given build.
            new RegistryValueStrategy(
                tweakId: Id,
                strategyName: "TaskbarIcon",
                hive: RegistryHive.CurrentUser,
                subKeyPath: @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                valueName: "ShowCopilotButton",
                enabledValue: 0,
                valueKind: RegistryValueKind.DWord,
                minBuildNumber: 22621
            )
        };

        public override TweakResult Apply()
        {
            var result = base.Apply();
            if (result.Success)
            {
                ShellRefreshHelper.RestartExplorer();
                result.Message += " (Explorer restarted to apply the visible change.)";
            }
            return result;
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