namespace ZeroBloat.Tweaks.Modules
{
    /// <summary>
    /// Disables the SysMain (Superfetch) service, which preloads frequently
    /// used application data into RAM. On systems with an HDD or under
    /// memory pressure, this can cause excessive disk activity; disabling
    /// it is a common, low-risk performance tweak.
    ///
    /// Chosen as the first ServiceTweakBase implementation because its
    /// effect is immediately visible in Task Manager → Services, unlike
    /// Classic Context Menu which only matters on Windows 11.
    /// </summary>
    public class DisableSysMainTweak : ServiceTweakBase
    {
        public override string Id => "disable_sysmain";
        public override string DisplayName => "Stop Excessive File Re-Reading (SysMain)";
        public override string Description =>
            "Disables the SysMain (Superfetch) service, which preloads " +
            "frequently used app data into RAM and can cause high disk " +
            "usage on some systems.";
        public override TweakRiskTier RiskTier => TweakRiskTier.Safe;

        protected override string ServiceName => "SysMain";
    }
}