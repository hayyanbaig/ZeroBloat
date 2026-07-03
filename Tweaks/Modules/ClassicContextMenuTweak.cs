using Microsoft.Win32;

namespace ZeroBloat.Tweaks.Modules
{
    /// <summary>
    /// Restores the classic Windows 10 style right-click context menu,
    /// removing the "Show more options" intermediate step Windows 11 added.
    ///
    /// This is our lowest-risk tweak: single key, well-documented, no
    /// service dependency, and stable across Windows 11 builds since launch.
    /// Chosen as the first tweak to wire end-to-end through the engine.
    ///
    /// Mechanism: creating an empty CLSID override key suppresses the new
    /// Windows 11 context menu handler, causing Explorer to fall back to
    /// the full classic menu immediately instead of behind "Show more options."
    /// </summary>
    public class ClassicContextMenuTweak : RegistryTweakBase
    {
        public override string Id => "classic_context_menu";
        public override string DisplayName => "Classic Context Menu";
        public override string Description =>
            "Restores the traditional Windows 10 style right-click menu, " +
            "removing the Windows 11 'Show more options' extra step.";
        public override TweakRiskTier RiskTier => TweakRiskTier.Standard;

        protected override RegistryHive Hive => RegistryHive.CurrentUser;
        protected override string SubKeyPath =>
            @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
        protected override string ValueName => ""; // default value of the key
        protected override RegistryValueKind ValueKind => RegistryValueKind.String;

        // Applying = setting the default value to an empty string.
        protected override object EnabledValue => "";

        // Reverting = deleting the override key entirely (Windows 11 default
        // behavior when this key doesn't exist). Represented here as null
        // marker; actual delete handled via override below.
        protected override object DefaultValue => "__DELETE_KEY__";

        public override TweakResult Revert()
        {
            // This tweak's revert path is "delete the key" rather than
            // "write a different value," so it overrides the base
            // implementation instead of relying on DefaultValue directly.
            try
            {
                using var baseKey = Registry.CurrentUser;
                baseKey.DeleteSubKeyTree(
                    @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}",
                    throwOnMissingSubKey: false);

                UndoLog.ClearPreState(Id);

                return new TweakResult
                {
                    Success = true,
                    TargetPath = $@"HKCU\{SubKeyPath}",
                    Message = "Reverted Classic Context Menu — Windows 11 default menu restored.",
                    NewValue = "(key removed)"
                };
            }
            catch (System.Exception ex)
            {
                return new TweakResult
                {
                    Success = false,
                    Message = $"Failed to revert Classic Context Menu: {ex.Message}"
                };
            }
        }

        public override TweakCompatibility CheckCompatibility()
        {
            var info = Services.SystemInfo.Instance;

            // This override key only matters on Windows 11 (build 22000+).
            // On Windows 10, the classic menu is already the default, so
            // this tweak is meaningless there — flagged as unsupported
            // rather than silently no-op'ing.
            return info.BuildNumber >= 22000
                ? TweakCompatibility.Supported
                : TweakCompatibility.Unsupported;
        }
    }
}