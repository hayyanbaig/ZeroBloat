using ZeroBloat.Services.Manifest;
using ZeroBloat.Services;

namespace ZeroBloat.Tweaks
{
    /// <summary>
    /// Manifest-driven service tweak: stop + disable a named Windows
    /// service, revert restores the recorded pre-state. Used for any
    /// tweak whose "action" in tweaks.json is "stopAndDisable".
    /// </summary>
    public class GenericServiceTweak : ServiceTweakBase
    {
        private readonly TweakDefinition _def;

        public GenericServiceTweak(TweakDefinition def)
        {
            _def = def;
        }

        public override string Id => _def.Id;
        public override string DisplayName => _def.DisplayName;
        public override string Description => _def.Description;
        public override TweakRiskTier RiskTier =>
            System.Enum.TryParse<TweakRiskTier>(_def.RiskTier, out var tier) ? tier : TweakRiskTier.Standard;

        protected override string ServiceName => _def.ServiceName ?? string.Empty;

        public override TweakCompatibility CheckCompatibility()
        {
            var baseResult = base.CheckCompatibility(); // checks the service actually exists
            if (baseResult == TweakCompatibility.Unsupported)
                return TweakCompatibility.Unsupported;

            var info = SystemInfo.Instance;
            var rule = _def.Compatibility;

            if (rule == null)
                return TweakCompatibility.Supported;

            if (info.BuildNumber < rule.MinBuildNumber)
                return TweakCompatibility.Unsupported;

            if (rule.MaxBuildNumber.HasValue && info.BuildNumber > rule.MaxBuildNumber.Value)
                return TweakCompatibility.Unsupported;

            if (rule.Editions.Count > 0 && !rule.Editions.Contains(info.Edition.ToString()))
                return TweakCompatibility.Unsupported;

            return TweakCompatibility.Supported;
        }
    }
}