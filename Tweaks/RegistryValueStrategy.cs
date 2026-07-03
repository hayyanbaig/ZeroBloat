using System;
using Microsoft.Win32;

namespace ZeroBloat.Tweaks
{
    /// <summary>
    /// One registry-based strategy for a multi-strategy tweak. Each
    /// strategy tracks its own pre-state independently (keyed by
    /// "{tweakId}::{strategyName}") since different strategies for the
    /// same tweak may touch completely different registry locations.
    /// </summary>
    public class RegistryValueStrategy : ITweakStrategy
    {
        private readonly string _tweakId;
        private readonly RegistryHive _hive;
        private readonly string _subKeyPath;
        private readonly string _valueName;
        private readonly object _enabledValue;
        private readonly RegistryValueKind _valueKind;
        private readonly int _minBuildNumber;

        public string StrategyName { get; }

        public RegistryValueStrategy(
            string tweakId, string strategyName, RegistryHive hive, string subKeyPath,
            string valueName, object enabledValue, RegistryValueKind valueKind,
            int minBuildNumber = 0)
        {
            _tweakId = tweakId;
            StrategyName = strategyName;
            _hive = hive;
            _subKeyPath = subKeyPath;
            _valueName = valueName;
            _enabledValue = enabledValue;
            _valueKind = valueKind;
            _minBuildNumber = minBuildNumber;
        }

        private string UndoKey => $"{_tweakId}::{StrategyName}";

        public bool IsApplicableToBuild(int buildNumber) => buildNumber >= _minBuildNumber;

        private RegistryKey OpenBaseKey() => _hive switch
        {
            RegistryHive.LocalMachine => Registry.LocalMachine,
            RegistryHive.CurrentUser => Registry.CurrentUser,
            _ => throw new NotSupportedException($"Hive {_hive} not supported")
        };

        private object? ReadCurrentValue()
        {
            using var baseKey = OpenBaseKey();
            using var key = baseKey.OpenSubKey(_subKeyPath, writable: false);
            return key?.GetValue(_valueName);
        }

        public TweakResult Apply()
        {
            try
            {
                var oldValue = ReadCurrentValue();
                using var baseKey = OpenBaseKey();
                using var key = baseKey.CreateSubKey(_subKeyPath, writable: true)
                    ?? throw new InvalidOperationException($"Could not open/create {_subKeyPath}");

                key.SetValue(_valueName, _enabledValue, _valueKind);

                UndoLog.RecordPreState(UndoKey, oldValue?.ToString());

                return new TweakResult
                {
                    Success = true,
                    TargetPath = $@"{_hive}\{_subKeyPath}\{_valueName}",
                    OldValue = oldValue?.ToString() ?? "(not set)",
                    NewValue = _enabledValue.ToString(),
                    Message = $"Strategy '{StrategyName}' applied."
                };
            }
            catch (Exception ex)
            {
                return new TweakResult { Success = false, Message = $"Strategy '{StrategyName}' failed: {ex.Message}" };
            }
        }

        public TweakResult Verify()
        {
            var current = ReadCurrentValue();
            bool matches = current != null && current.Equals(_enabledValue);
            return new TweakResult
            {
                Success = matches,
                Message = matches
                    ? $"Strategy '{StrategyName}' still enforced."
                    : $"Strategy '{StrategyName}' drifted — expected {_enabledValue}, found {current?.ToString() ?? "(not set)"}."
            };
        }

        public TweakResult Revert()
        {
            try
            {
                bool hadEntry = UndoLog.TryGetPreState(UndoKey, out var stored);
                using var baseKey = OpenBaseKey();

                if (hadEntry && stored == null)
                {
                    using var key = baseKey.OpenSubKey(_subKeyPath, writable: true);
                    key?.DeleteValue(_valueName, throwOnMissingValue: false);
                }
                else
                {
                    using var key = baseKey.CreateSubKey(_subKeyPath, writable: true)
                        ?? throw new InvalidOperationException($"Could not open {_subKeyPath}");
                    // No safe generic "default" to fall back to per-strategy;
                    // if nothing was recorded, leave the value as-is rather
                    // than guessing — caller (MultiStrategyTweakBase) treats
                    // this as best-effort.
                    if (hadEntry)
                        key.SetValue(_valueName, stored!, RegistryValueKind.String);
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