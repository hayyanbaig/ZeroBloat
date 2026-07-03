using System.Collections.Generic;
using Microsoft.Win32;
using ZeroBloat.Services;

namespace ZeroBloat.Tweaks.Modules
{
    /// <summary>
    /// Disables Windows Recall / Click to Do. This is the highest-churn
    /// tweak in the whole app — Recall is Microsoft's most actively
    /// developed feature right now, and the mechanism for disabling it
    /// has already shifted since launch. Uses two independent strategies
    /// so one Windows Update closing one route doesn't fully break this.
    ///
    /// IMPORTANT: the exact registry path and service name below are
    /// based on publicly documented Recall policy controls at the time
    /// this was written — NOT independently verified against a live
    /// Copilot+ PC with Recall present, since dev/test hardware doesn't
    /// have Recall available. Treat this as "should work, needs real
    /// hardware verification" rather than proven, same caveat as the
    /// rest of this batch before it was individually tested.
    /// </summary>
    public class DisableRecallTweak : MultiStrategyTweakBase
    {
        public override string Id => "disable_recall";
        public override string DisplayName => "Nuke Windows Recall / Click to Do";
        public override string Description =>
            "Disables Windows Recall's background snapshotting and AI data " +
            "analysis. Uses multiple fallback strategies since Microsoft " +
            "actively changes how Recall is controlled between builds.";
        public override TweakRiskTier RiskTier => TweakRiskTier.Standard;

        protected override int CurrentBuildNumber => SystemInfo.Instance.BuildNumber;

        public override IReadOnlyList<ITweakStrategy> Strategies => new List<ITweakStrategy>
        {
            // Primary: the documented Group Policy control for disabling
            // Recall / "AI Data Analysis" at the OS level.
            new RegistryValueStrategy(
                tweakId: Id,
                strategyName: "PolicyKey",
                hive: RegistryHive.LocalMachine,
                subKeyPath: @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                valueName: "DisableAIDataAnalysis",
                enabledValue: 1,
                valueKind: RegistryValueKind.DWord,
                minBuildNumber: 26100 // Recall shipped starting around this build range
            ),

            // Fallback: stop the background service Recall's snapshotting
            // depends on, in case the policy key alone doesn't fully
            // suppress it on a given build (or gets ignored after an update).
            new ServiceStopStrategy(
                tweakId: Id,
                strategyName: "ServiceStop",
                serviceName: "AIDataAnalysisSvc",
                minBuildNumber: 26100
            )
        };
    }
}