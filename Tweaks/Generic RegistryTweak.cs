using Microsoft.Win32;
using ZeroBloat.Services.Manifest;
using ZeroBloat.Services;

namespace ZeroBloat.Tweaks
{
    /// <summary>
    /// Manifest-driven registry tweak for the common case: write a single
    /// value, revert by restoring the recorded pre-state (or a hardcoded
    /// default). Used for any tweak whose "action" in tweaks.json is
    /// "setValue" — most registry tweaks fit this shape.
    ///
    /// Tweaks needing different revert behavior (e.g. delete the key
    /// instead of writing a value, like Classic Context Menu) still get
    /// their own hardcoded class instead of using this generic one —
    /// see TweakFactory for how that split is decided.
    /// </summary>
    public class GenericRegistryTweak : RegistryTweakBase
    {
        private readonly TweakDefinition _def;

        public GenericRegistryTweak(TweakDefinition def)
        {
            _def = def;
        }

        public override string Id => _def.Id;
        public override string DisplayName => _def.DisplayName;
        public override string Description => _def.Description;
        public override TweakRiskTier RiskTier =>
            System.Enum.TryParse<TweakRiskTier>(_def.RiskTier, out var tier) ? tier : TweakRiskTier.Standard;

        protected override RegistryHive Hive =>
            System.Enum.TryParse<RegistryHive>(_def.Hive, out var hive) ? hive : RegistryHive.CurrentUser;

        protected override string SubKeyPath => _def.SubKeyPath ?? string.Empty;
        protected override string ValueName => _def.ValueName ?? string.Empty;

        protected override RegistryValueKind ValueKind =>
            System.Enum.TryParse<RegistryValueKind>(_def.ValueKind, out var kind) ? kind : RegistryValueKind.DWord;

        protected override object EnabledValue => _def.EnabledValue ?? 1;
        protected override object DefaultValue => _def.DefaultValue ?? 0;

        public override TweakCompatibility CheckCompatibility()
        {
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