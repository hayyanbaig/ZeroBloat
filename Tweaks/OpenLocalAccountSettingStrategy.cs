using System;
using System.Diagnostics;

namespace ZeroBloat.Tweaks
{
    /// <summary>
    /// Opens Windows' own "Sign in with a local account instead" settings
    /// page rather than programmatically rewriting identity/account
    /// registry structures directly.
    ///
    /// DELIBERATE DESIGN DECISION: this is the highest-risk tweak in the
    /// entire app. Directly manipulating MSA-to-local account linkage in
    /// the registry (UserExtendedProperties, IdentityCRL cache, etc.)
    /// without going through Windows' own account-switch flow risks
    /// corrupting the user's profile or locking them out of sign-in —
    /// Microsoft itself gates this behind an interactive flow requiring
    /// password/PIN reconfirmation for exactly that reason. A "silent,
    /// one-click" implementation of this specific tweak would be reckless
    /// regardless of how many fallback strategies wrap it.
    ///
    /// So: this strategy launches the real Windows settings page and lets
    /// the user complete the switch themselves, with Windows' own safety
    /// checks intact. It's honest about not being fully automated — the
    /// Apply() result message says so explicitly rather than claiming
    /// success it can't back up.
    /// </summary>
    public class OpenLocalAccountSettingsStrategy : ITweakStrategy
    {
        private readonly int _minBuildNumber;

        public string StrategyName => "GuidedSettingsFlow";

        public OpenLocalAccountSettingsStrategy(int minBuildNumber = 0)
        {
            _minBuildNumber = minBuildNumber;
        }

        public bool IsApplicableToBuild(int buildNumber) => buildNumber >= _minBuildNumber;

        public TweakResult Apply()
        {
            try
            {
                // ms-settings deep link to the account type page. Works on
                // Windows 10 and 11; exact page reached may vary slightly
                // by build, but this URI scheme has been stable since
                // Windows 10 and is Microsoft's own supported entry point.
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:yourinfo",
                    UseShellExecute = true
                });

                return new TweakResult
                {
                    Success = true,
                    Message = "Opened Windows Account settings. ZeroBloat does not " +
                              "automate this step directly — switching from a Microsoft " +
                              "account to a local account involves password/PIN " +
                              "reconfirmation that only Windows' own flow can safely " +
                              "handle. Complete the switch there, then return here."
                };
            }
            catch (Exception ex)
            {
                return new TweakResult
                {
                    Success = false,
                    Message = $"Could not open Account settings: {ex.Message}"
                };
            }
        }

        public TweakResult Verify()
        {
            // Best-effort heuristic: presence of this registry key
            // generally indicates the account is currently linked to a
            // Microsoft Account. Its absence is a reasonable signal (not
            // a guarantee) that the account is local. Treat this as
            // informational, not authoritative — surfaced to the user as
            // such rather than a hard pass/fail.
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\IdentityCRL\UserExtendedProperties", writable: false);

                bool likelyStillLinked = key != null;

                return new TweakResult
                {
                    Success = !likelyStillLinked,
                    Message = likelyStillLinked
                        ? "Account appears to still be linked to a Microsoft account " +
                          "(heuristic check, not authoritative)."
                        : "No Microsoft account linkage detected (heuristic check)."
                };
            }
            catch (Exception ex)
            {
                return new TweakResult
                {
                    Success = false,
                    Message = $"Could not check account linkage state: {ex.Message}"
                };
            }
        }

        public TweakResult Revert()
        {
            return new TweakResult
            {
                Success = true,
                Message = "No automated revert available — switching back to a Microsoft " +
                          "account requires signing in again through Windows' own Account " +
                          "settings, the same as the original switch."
            };
        }
    }
}