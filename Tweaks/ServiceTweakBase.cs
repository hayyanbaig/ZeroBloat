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

            // Format stored by Apply(): "priorStatusValue;priorStartValue"
            // e.g. "4;2" means prior ServiceControllerStatus was Running (4),
            // prior Start registry value was Automatic (2).
            var storedPreState = UndoLog.GetPreState(Id);

            int restoreStartValue = StartValueDefault;
            bool shouldBeRunning = true;

            if (storedPreState != null)
            {
                var parts = storedPreState.Split(';');
                if (parts.Length == 2)
                {
                    if (int.TryParse(parts[1], out var parsedStart))
                        restoreStartValue = parsedStart;

                    if (int.TryParse(parts[0], out var parsedStatus))
                    {
                        // ServiceControllerStatus: Stopped = 1
                        shouldBeRunning = parsedStatus != (int)ServiceControllerStatus.Stopped;
                    }
                }
            }

            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(StartTypeRegistryPath, writable: true))
                {
                    key?.SetValue("Start", restoreStartValue, RegistryValueKind.DWord);
                }

                if (shouldBeRunning)
                {
                    using var sc = new ServiceController(ServiceName);
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                    }
                }

                sw.Stop();
                UndoLog.ClearPreState(Id);

                return new TweakResult
                {
                    Success = true,
                    TargetPath = $@"Service: {ServiceName}",
                    NewValue = $"Start: {restoreStartValue}, Running: {shouldBeRunning}",
                    Duration = sw.Elapsed,
                    Message = $"Reverted {DisplayName} — restored recorded pre-state " +
                              $"({(storedPreState != null ? "from undo log" : "using default, no prior state found")})."
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