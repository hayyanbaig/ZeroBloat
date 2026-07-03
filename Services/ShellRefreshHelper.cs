using System.Diagnostics;
using System.Threading;

namespace ZeroBloat.Services
{
    /// <summary>
    /// Restarts Windows Explorer (explorer.exe) so shell-level registry
    /// changes (context menu handlers, taskbar icon visibility, etc.) take
    /// visible effect immediately instead of waiting for the next sign-in.
    ///
    /// IMPORTANT: this closes ALL open File Explorer windows, not just the
    /// taskbar/desktop. Only call this from tweaks that genuinely need a
    /// shell refresh, and — once the UI exists — always ask the user
    /// before doing this rather than restarting silently, since closing
    /// their open folder windows without warning is a bad experience even
    /// if the underlying tweak worked correctly.
    /// </summary>
    public static class ShellRefreshHelper
    {
        public static void RestartExplorer()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = "/f /im explorer.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            })?.WaitForExit();

            Thread.Sleep(500);

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true
            });
        }
    }
}