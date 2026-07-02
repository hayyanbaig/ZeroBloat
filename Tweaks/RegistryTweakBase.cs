using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace ZeroBloat.Tweaks
{
    /// <summary>
    /// Shared implementation for the common case: a tweak that reads and
    /// writes a single registry DWORD value. Handles the boilerplate
    /// (open key, read/write, timing, error wrapping) so individual tweaks
    /// only need to declare WHAT to change, not HOW to change it.
    ///
    /// Tweaks with more complex logic (services, multiple keys, WMI) should
    /// implement ITweak directly instead of inheriting this.
    /// </summary>
    public abstract class RegistryTweakBase : ITweak
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public abstract TweakRiskTier RiskTier { get; }

        /// <summary>Full registry key path, e.g. @"HKCU\Software\Classes\CLSID\{...}\InprocServer32"</summary>
        protected abstract RegistryHive Hive { get; }
        protected abstract string SubKeyPath { get; }
        protected abstract string ValueName { get; }

        /// <summary>The value that represents the tweak being "applied" / turned on.</summary>
        protected abstract object EnabledValue { get; }

        /// <summary>The value that represents Windows default (used for Revert if no prior state was recorded).</summary>
        protected abstract object DefaultValue { get; }

        protected virtual RegistryValueKind ValueKind => RegistryValueKind.DWord;

        private RegistryKey OpenBaseKey()
        {
            return Hive switch
            {
                RegistryHive.LocalMachine => Registry.LocalMachine,
                RegistryHive.CurrentUser => Registry.CurrentUser,
                _ => throw new NotSupportedException($"Hive {Hive} not supported by RegistryTweakBase")
            };
        }

        private object? ReadCurrentValue()
        {
            using var baseKey = OpenBaseKey();
            using var key = baseKey.OpenSubKey(SubKeyPath, writable: false);
            return key?.GetValue(ValueName);
        }

        public virtual TweakResult PreviewChange()
        {
            var current = ReadCurrentValue();
            return new TweakResult
            {
                Success = true,
                TargetPath = $@"{Hive}\{SubKeyPath}\{ValueName}",
                OldValue = current?.ToString() ?? "(not set)",
                NewValue = EnabledValue.ToString(),
                Message = "Preview only — no changes made."
            };
        }

        public virtual TweakResult Apply()
        {
            var sw = Stopwatch.StartNew();
            var oldValue = ReadCurrentValue();

            try
            {
                using var baseKey = OpenBaseKey();
                using var key = baseKey.CreateSubKey(SubKeyPath, writable: true)
                    ?? throw new InvalidOperationException($"Could not open or create key: {SubKeyPath}");

                key.SetValue(ValueName, EnabledValue, ValueKind);
                sw.Stop();

                var result = new TweakResult
                {
                    Success = true,
                    TargetPath = $@"{Hive}\{SubKeyPath}\{ValueName}",
                    OldValue = oldValue?.ToString() ?? "(not set)",
                    NewValue = EnabledValue.ToString(),
                    Duration = sw.Elapsed,
                    Message = $"Applied {DisplayName}."
                };

                // Persist the pre-change value so Revert() and the granular
                // undo system can restore it later, independent of System
                // Restore. Actual DPAPI encryption happens in the undo
                // manifest writer, not here — this class just reports the
                // value that needs to be stored.
                UndoLog.RecordPreState(Id, oldValue?.ToString());

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new TweakResult
                {
                    Success = false,
                    TargetPath = $@"{Hive}\{SubKeyPath}\{ValueName}",
                    Duration = sw.Elapsed,
                    Message = $"Failed to apply {DisplayName}: {ex.Message}"
                };
            }
        }

        public virtual TweakResult Verify()
        {
            var current = ReadCurrentValue();
            bool matches = current != null && current.Equals(EnabledValue);

            return new TweakResult
            {
                Success = matches,
                TargetPath = $@"{Hive}\{SubKeyPath}\{ValueName}",
                OldValue = EnabledValue.ToString(),
                NewValue = current?.ToString() ?? "(not set)",
                Message = matches
                    ? $"{DisplayName} verified — still applied."
                    : $"{DisplayName} DRIFTED — expected {EnabledValue}, found {current?.ToString() ?? "(not set)"}."
            };
        }

        public virtual TweakResult Revert()
        {
            var sw = Stopwatch.StartNew();
            var storedPreState = UndoLog.GetPreState(Id);

            // Fall back to Windows default if no stored pre-state exists
            // (e.g. tweak was applied before the undo log existed, or the
            // log was cleared).
            object restoreValue = storedPreState != null
                ? storedPreState
                : DefaultValue;

            try
            {
                using var baseKey = OpenBaseKey();
                using var key = baseKey.CreateSubKey(SubKeyPath, writable: true)
                    ?? throw new InvalidOperationException($"Could not open key: {SubKeyPath}");

                key.SetValue(ValueName, restoreValue, ValueKind);
                sw.Stop();

                return new TweakResult
                {
                    Success = true,
                    TargetPath = $@"{Hive}\{SubKeyPath}\{ValueName}",
                    NewValue = restoreValue.ToString(),
                    Duration = sw.Elapsed,
                    Message = $"Reverted {DisplayName}."
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
            // Default: assume supported. Override in individual tweaks
            // that have known edition/build restrictions.
            return TweakCompatibility.Supported;
        }
    }

    /// <summary>
    /// Placeholder for the DPAPI-secured undo log. Replaced with the real
    /// encrypted read/write implementation once the undo system is built —
    /// stubbed here so RegistryTweakBase compiles and is testable now.
    /// </summary>
    internal static class UndoLog
    {
        public static void RecordPreState(string tweakId, string? value)
        {
            // TODO: replace with DPAPI-encrypted write to the transaction manifest.
            Debug.WriteLine($"[UndoLog] Would record: {tweakId} -> {value ?? "(not set)"}");
        }

        public static string? GetPreState(string tweakId)
        {
            // TODO: replace with DPAPI-encrypted read from the transaction manifest.
            Debug.WriteLine($"[UndoLog] Would read pre-state for: {tweakId}");
            return null;
        }
    }
}