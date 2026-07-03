using System;
using System.Diagnostics;

namespace ZeroBloat.Tweaks
{
    /// <summary>
    /// Removes an installed app package (AppX/MSIX) via PowerShell's
    /// Remove-AppxPackage. Used as a fallback for Copilot specifically,
    /// since the legacy TurnOffWindowsCopilot policy is unreliable in
    /// practice and Microsoft has flagged it for near-term deprecation
    /// in favor of AppLocker-based blocking. Uninstalling the app package
    /// directly is currently the most dependable method — it just doesn't
    /// prevent Windows from silently reinstalling it on a future update,
    /// which is why this is paired with the policy-key strategy as a
    /// fallback pair, not a replacement for it.
    /// </summary>
    public class AppPackageRemoveStrategy : ITweakStrategy
    {
        private readonly string _packageNamePattern;
        private readonly int _minBuildNumber;

        public string StrategyName { get; }

        public AppPackageRemoveStrategy(string strategyName, string packageNamePattern, int minBuildNumber = 0)
        {
            StrategyName = strategyName;
            _packageNamePattern = packageNamePattern;
            _minBuildNumber = minBuildNumber;
        }

        public bool IsApplicableToBuild(int buildNumber) => buildNumber >= _minBuildNumber;

        private TweakResult RunPowerShell(string script, string successMessage)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process!.WaitForExit(20000);
                string error = process.StandardError.ReadToEnd();

                bool success = process.ExitCode == 0 && string.IsNullOrWhiteSpace(error);

                return new TweakResult
                {
                    Success = success,
                    Message = success ? successMessage : $"PowerShell error: {error}"
                };
            }
            catch (Exception ex)
            {
                return new TweakResult { Success = false, Message = $"Failed to run PowerShell: {ex.Message}" };
            }
        }

        public TweakResult Apply()
        {
            // AllUsers removal requires admin — app already runs elevated
            // per app.manifest.
            var script = $"Get-AppxPackage -AllUsers | Where-Object Name -ilike '*{_packageNamePattern}*' | " +
                         "ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction SilentlyContinue }";

            return RunPowerShell(script, $"Strategy '{StrategyName}' applied — matching app package(s) removed.");
        }

        public TweakResult Verify()
        {
            var script = $"(Get-AppxPackage -AllUsers | Where-Object Name -ilike '*{_packageNamePattern}*').Count";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                using var process = Process.Start(psi);
                string output = process!.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(10000);

                bool stillInstalled = !string.IsNullOrEmpty(output) && output != "0";

                return new TweakResult
                {
                    Success = !stillInstalled,
                    Message = stillInstalled
                        ? $"Strategy '{StrategyName}' drifted — package matching '{_packageNamePattern}' is installed again."
                        : $"Strategy '{StrategyName}' still enforced — no matching package installed."
                };
            }
            catch (Exception ex)
            {
                return new TweakResult { Success = false, Message = $"Verify failed: {ex.Message}" };
            }
        }

        public TweakResult Revert()
        {
            // App packages removed this way generally can't be cleanly
            // reinstalled offline — reversing this means letting Windows
            // Update reintroduce it, or reinstalling from the Store.
            return new TweakResult
            {
                Success = true,
                Message = $"Strategy '{StrategyName}': no automated reinstall available. " +
                          "The app package was removed via uninstall, not a reversible setting — " +
                          "reinstall from the Microsoft Store if you want it back."
            };
        }
    }
}