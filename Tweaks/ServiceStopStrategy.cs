using System;
using System.ServiceProcess;
using Microsoft.Win32;

namespace ZeroBloat.Tweaks
{
    /// <summary>
    /// One service-stop-based strategy for a multi-strategy tweak, e.g.
    /// stopping a background service Recall depends on as a fallback if
    /// the primary policy-key strategy doesn't fully suppress it.
    /// </summary>
    public class ServiceStopStrategy : ITweakStrategy
    {
        private readonly string _tweakId;
        private readonly string _serviceName;
        private readonly int _minBuildNumber;
        private const int StartValueDisabled = 4;
        private const int StartValueDefault = 2;

        public string StrategyName { get; }

        public ServiceStopStrategy(string tweakId, string strategyName, string serviceName, int minBuildNumber = 0)
        {
            _tweakId = tweakId;
            StrategyName = strategyName;
            _serviceName = serviceName;
            _minBuildNumber = minBuildNumber;
        }

        private string UndoKey => $"{_tweakId}::{StrategyName}";
        private string StartTypePath => $@"SYSTEM\CurrentControlSet\Services\{_serviceName}";

        public bool IsApplicableToBuild(int buildNumber) => buildNumber >= _minBuildNumber;

        private ServiceControllerStatus? GetStatus()
        {
            try
            {
                using var sc = new ServiceController(_serviceName);
                return sc.Status;
            }
            catch (InvalidOperationException)
            {
                return null; // service doesn't exist on this system
            }
        }

        public TweakResult Apply()
        {
            var status = GetStatus();
            if (status == null)
                return new TweakResult { Success = false, Message = $"Strategy '{StrategyName}': service '{_serviceName}' not found." };

            try
            {
                using (var sc = new ServiceController(_serviceName))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                    }
                }

                using (var key = Registry.LocalMachine.OpenSubKey(StartTypePath, writable: true))
                {
                    key?.SetValue("Start", StartValueDisabled, RegistryValueKind.DWord);
                }

                UndoLog.RecordPreState(UndoKey, $"{(int)status};{StartValueDefault}");

                return new TweakResult { Success = true, Message = $"Strategy '{StrategyName}' applied — {_serviceName} stopped." };
            }
            catch (Exception ex)
            {
                return new TweakResult { Success = false, Message = $"Strategy '{StrategyName}' failed: {ex.Message}" };
            }
        }

        public TweakResult Verify()
        {
            var status = GetStatus();
            bool stopped = status == ServiceControllerStatus.Stopped;
            return new TweakResult
            {
                Success = stopped,
                Message = stopped
                    ? $"Strategy '{StrategyName}' still enforced — {_serviceName} stopped."
                    : $"Strategy '{StrategyName}' drifted — {_serviceName} is now {status}."
            };
        }

        public TweakResult Revert()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(StartTypePath, writable: true))
                {
                    key?.SetValue("Start", StartValueDefault, RegistryValueKind.DWord);
                }

                using (var sc = new ServiceController(_serviceName))
                {
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                    }
                }

                UndoLog.ClearPreState(UndoKey);
                return new TweakResult { Success = true, Message = $"Strategy '{StrategyName}' reverted." };
            }
            catch (Exception ex)
            {
                return new TweakResult { Success = false, Message = $"Strategy '{StrategyName}' revert failed: {ex.Message}" };
            }
        }
    }
}