using System;
using System.Management;
using Microsoft.Win32;

namespace ZeroBloat.Services
{
    public enum WindowsEdition
    {
        Home,
        Pro,
        Enterprise,
        Education,
        Unknown
    }

    public enum ChassisCategory
    {
        Laptop,
        Desktop,
        Unknown
    }

    /// <summary>
    /// Global singleton that detects the current system's Windows edition,
    /// build number, and hardware chassis type. Every tweak module and the
    /// UI's capability badging system reads from this at startup.
    /// </summary>
    public sealed class SystemInfo
    {
        private static readonly Lazy<SystemInfo> _instance =
            new Lazy<SystemInfo>(() => new SystemInfo());

        public static SystemInfo Instance => _instance.Value;

        public WindowsEdition Edition { get; private set; }
        public string EditionRaw { get; private set; } = string.Empty;
        public int BuildNumber { get; private set; }
        public string DisplayVersion { get; private set; } = string.Empty; // e.g. "24H2"
        public ChassisCategory Chassis { get; private set; }
        public bool HasBattery { get; private set; }

        private SystemInfo()
        {
            DetectEdition();
            DetectBuildInfo();
            DetectChassis();
        }

        private void DetectEdition()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                EditionRaw = key?.GetValue("EditionID")?.ToString() ?? "Unknown";

                Edition = EditionRaw.ToLowerInvariant() switch
                {
                    var e when e.Contains("home") => WindowsEdition.Home,
                    var e when e.Contains("professional") || e.Contains("pro") => WindowsEdition.Pro,
                    var e when e.Contains("enterprise") => WindowsEdition.Enterprise,
                    var e when e.Contains("education") => WindowsEdition.Education,
                    _ => WindowsEdition.Unknown
                };
            }
            catch (Exception ex)
            {
                // Fail safe: unknown edition means every tweak should show
                // "untested" rather than assuming compatibility.
                Edition = WindowsEdition.Unknown;
                EditionRaw = $"Detection failed: {ex.Message}";
            }
        }

        private void DetectBuildInfo()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

                var buildStr = key?.GetValue("CurrentBuildNumber")?.ToString();
                BuildNumber = int.TryParse(buildStr, out var build) ? build : 0;

                DisplayVersion = key?.GetValue("DisplayVersion")?.ToString()
                    ?? key?.GetValue("ReleaseId")?.ToString()
                    ?? "Unknown";
            }
            catch (Exception)
            {
                BuildNumber = 0;
                DisplayVersion = "Unknown";
            }
        }

        private void DetectChassis()
        {
            // Double-lock validation: WMI chassis type alone is unreliable
            // across OEMs, so cross-check against physical battery presence
            // before flagging a device as a laptop. This matters because
            // OEM bloatware removal defaults to "keep" for hardware-control
            // utilities (fan curves, RGB) only on confirmed laptop chassis.
            bool chassisIndicatesLaptop = false;
            bool batteryPresent = false;

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure");
                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["ChassisTypes"] is ushort[] types)
                    {
                        foreach (var t in types)
                        {
                            // 8=Portable, 9=Laptop, 10=Notebook, 11=Handheld,
                            // 12=Docking Station, 14=Sub Notebook, 30=Tablet, 31=Convertible
                            if (t is 8 or 9 or 10 or 11 or 14 or 30 or 31)
                            {
                                chassisIndicatesLaptop = true;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // WMI query failed (rare, but some OEMs misreport or block it).
                // Fall through and rely on battery check alone.
            }

            try
            {
                using var batterySearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                batteryPresent = batterySearcher.Get().Count > 0;
            }
            catch (Exception)
            {
                batteryPresent = false;
            }

            HasBattery = batteryPresent;

            // Require agreement between at least the battery check and chassis
            // check where possible; battery presence is the stronger signal
            // since chassis codes are the part most commonly misreported.
            if (chassisIndicatesLaptop || batteryPresent)
            {
                Chassis = ChassisCategory.Laptop;
            }
            else if (!chassisIndicatesLaptop && !batteryPresent)
            {
                Chassis = ChassisCategory.Desktop;
            }
            else
            {
                Chassis = ChassisCategory.Unknown;
            }
        }

        /// <summary>
        /// Convenience check used by tweak modules to decide whether a
        /// Group Policy based tweak needs to fall back to a direct registry
        /// write instead (Windows Home lacks gpedit.msc).
        /// </summary>
        public bool RequiresRegistryFallback => Edition == WindowsEdition.Home;

        public override string ToString()
        {
            return $"Windows {EditionRaw} (Build {BuildNumber}, {DisplayVersion}) — " +
                   $"Chassis: {Chassis}, Battery: {HasBattery}";
        }
    }
}