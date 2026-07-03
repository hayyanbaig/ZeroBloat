using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZeroBloat.Tweaks
{
    /// <summary>
    /// Records which strategy name succeeded when a multi-strategy tweak
    /// was applied, so Revert() knows exactly what to undo instead of
    /// guessing or reverting every strategy blindly. Separate file from
    /// the main UndoLog since the data shape is different (strategy name,
    /// not a registry pre-value).
    /// </summary>
    internal static class MultiStrategyUndoLog
    {
        private static readonly string StorageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZeroBloat");

        private static readonly string LogPath = Path.Combine(StorageDir, "strategy_log.dat");

        private static Dictionary<string, string>? _cache;
        private static readonly object _lock = new();

        private static Dictionary<string, string> LoadCache()
        {
            lock (_lock)
            {
                if (_cache != null)
                    return _cache;

                if (!File.Exists(LogPath))
                {
                    _cache = new Dictionary<string, string>();
                    return _cache;
                }

                try
                {
                    var encrypted = File.ReadAllBytes(LogPath);
                    var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                    var json = Encoding.UTF8.GetString(decrypted);
                    _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                             ?? new Dictionary<string, string>();
                }
                catch (Exception)
                {
                    _cache = new Dictionary<string, string>();
                }

                return _cache;
            }
        }

        private static void SaveCache(Dictionary<string, string> cache)
        {
            lock (_lock)
            {
                Directory.CreateDirectory(StorageDir);
                var json = JsonSerializer.Serialize(cache);
                var plain = Encoding.UTF8.GetBytes(json);
                var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(LogPath, encrypted);
            }
        }

        public static void RecordUsedStrategy(string tweakId, string strategyName)
        {
            var cache = LoadCache();
            cache[tweakId] = strategyName;
            SaveCache(cache);
        }

        public static string? GetUsedStrategy(string tweakId)
        {
            var cache = LoadCache();
            return cache.TryGetValue(tweakId, out var name) ? name : null;
        }

        public static void ClearUsedStrategy(string tweakId)
        {
            var cache = LoadCache();
            if (cache.Remove(tweakId))
                SaveCache(cache);
        }
    }
}