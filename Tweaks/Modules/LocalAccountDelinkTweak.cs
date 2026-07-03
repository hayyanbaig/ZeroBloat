using System.Collections.Generic;
using ZeroBloat.Services;

namespace ZeroBloat.Tweaks.Modules
{
    /// <summary>
    /// Guides the user through detaching their Microsoft account from this
    /// Windows profile. Deliberately NOT fully automated — see
    /// OpenLocalAccountSettingsStrategy for the reasoning. This is the
    /// highest-maintenance, highest-risk tweak in ZeroBloat's whole
    /// feature set; expect to revisit it after nearly every major Windows
    /// feature update, per the original risk assessment.
    /// </summary>
    public class LocalAccountDelinkTweak : MultiStrategyTweakBase
    {
        public override string Id => "local_account_delink";
        public override string DisplayName => "Local Account De-Link";
        public override string Description =>
            "Guides you through switching from a Microsoft account to a " +
            "local account using Windows' own account settings flow.";
        public override TweakRiskTier RiskTier => TweakRiskTier.Standard;

        protected override int CurrentBuildNumber => SystemInfo.Instance.BuildNumber;

        public override IReadOnlyList<ITweakStrategy> Strategies => new List<ITweakStrategy>
        {
            new OpenLocalAccountSettingsStrategy(minBuildNumber: 0)
        };
    }
}