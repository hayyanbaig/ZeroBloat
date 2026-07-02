using System;
using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;

namespace ZeroBloat.Tweaks
{
    /// <summary>
    /// Shared implementation for tweaks that stop a Windows service and
    /// prevent it from restarting, e.g. SysMain/Superfetch. Also sets the
    /// service's Start registry value so the change survives a reboot,
    /// since stopping a running service alone doesn't stop it from
    /// restarting at next boot.
    /// </summary>
    public abstract class ServiceTweakBase : ITweak
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public abstract TweakRiskTier RiskTier { get; }

        /// <summary>Windows service short name, e.g. "SysMain"</summary>
        protected abstract string ServiceName { get; }

        // Start value meanings: 2 = Automatic, 3 = Manual, 4 = Disabled
        private const int StartValueDisabled = 4;
        private const int StartValueDefault = 2; // most services we target default to Automatic

        private string StartTypeRegistryPath =>
            $@"SYSTEM\CurrentControlSet\Services\{ServiceName}";

        private int? ReadStartValue()
        {
            using var key = Registry.LocalMachine.OpenSubKey(StartTypeRegistryPath, writable: false);
            var val = key?.GetValue("Start");
            return val is int i ? i : null;
        }

        private ServiceControllerStatus? GetCurrentStatus()
        {
            try
            {
                using var sc = new ServiceController(ServiceName);
                return sc.Status;
            }
            catch (InvalidOperationException)
            {
                // Service doesn't exist on this system.
                return null;
            }
        }

        public virtual TweakResult PreviewChange()
        {
            var status = GetCurrentStatus();
            var startVal = ReadStartValue();

            return new TweakResult
            {
                Success = true,
                TargetPath = $@"Service: {ServiceName}",
                OldValue = $"Status: {status?.ToString() ?? "Not found"}, Start: {startVal?.ToString() ?? "Unknown"}",
                NewValue = "Status: Stopped, Start: Disabled",
                Message = "Preview only — no changes made."
            };
        }

        public virtual TweakResult Apply()
        {
            var sw = Stopwatch.StartNew();
            var priorStatus = GetCurrentStatus();
            var priorStartVal = ReadStartValue();

            try
            {
                using (var sc = new ServiceController(ServiceName))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                    }
                }

                using (var key = Registry.LocalMachine.OpenSubKey(StartTypeRegistryPath, writable: true))
                {
                    key?.SetValue("Start", StartValueDisabled, RegistryValueKind.DWord);
                }

                sw.Stop();

                UndoLog.RecordPreState(Id, $"{(int?)priorStatus};{priorStartVal}");

                return new TweakResult
                {
                    Success = true,
                    TargetPath = $@"Service: {ServiceName}",
                    OldValue = $"Status: {priorStatus}, Start: {priorStartVal}",
                    NewValue = "Status: Stopped, Start: Disabled",
                    Duration = sw.Elapsed,
                    Message = $"Applied {DisplayName} — {ServiceName} stopped and disabled."
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new TweakResult
                {
                    Success = false,
                    TargetPath = $@"Service: {ServiceName}",
                    Duration = sw.Elapsed,
                    Message = $"Failed to apply {DisplayName}: {ex.Message}"
                };
            }
        }

        public virtual TweakResult Verify()
        {
            var status = GetCurrentStatus();
            var startVal = ReadStartValue();

            bool matches = status == ServiceControllerStatus.Stopped
                           && startVal == StartValueDisabled;

            return new TweakResult
            {
                Success = matches,
                TargetPath = $@"Service: {ServiceName}",
                OldValue = "Status: Stopped, Start: Disabled",
                NewValue = $"Status: {status}, Start: {startVal}",
                Message = matches
                    ? $"{DisplayName} verified — {ServiceName} still stopped and disabled."
                    : $"{DisplayName} DRIFTED — {ServiceName} is now Status: {status}, Start: {startVal}."
            };
        }

        public virtual TweakResult Revert()
        {
            var sw = Stopwatch.StartNew();

            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(StartTypeRegistryPath, writable: true))
                {
                    key?.SetValue("Start", StartValueDefault, RegistryValueKind.DWord);
                }

                using (var sc = new ServiceController(ServiceName))
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                }

                sw.Stop();

                return new TweakResult
                {
                    Success = true,
                    TargetPath = $@"Service: {ServiceName}",
                    NewValue = "Status: Running, Start: Automatic",
                    Duration = sw.Elapsed,
                    Message = $"Reverted {DisplayName} — {ServiceName} restarted and set to Automatic."
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new TweakResult
                {
                    Success = false,
                    Duration = sw.Elapsed,
                    Message = $"Failed to revert {DisplayName}: {ex.Message}"
                };
            }
        }

        public virtual TweakCompatibility CheckCompatibility()
        {
            return GetCurrentStatus() != null
                ? TweakCompatibility.Supported
                : TweakCompatibility.Unsupported;
        }
    }
}