using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZeroBloat.Tweaks
{
    /// <summary>
    /// Persists the pre-change value of every applied tweak to disk,
    /// encrypted with DPAPI (user-scope). This is what makes granular,
    /// per-tweak Revert() work independently of System Restore — even on
    /// machines where System Restore is disabled by Group Policy.
    ///
    /// File lives in %LocalAppData%\ZeroBloat\undo_manifest.dat, encrypted
    /// so its contents aren't readable outside this Windows user account.
    /// </summary>
    internal static class UndoLog
    {
        private static readonly string StorageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZeroBloat");

        private static readonly string ManifestPath = Path.Combine(StorageDir, "undo_manifest.dat");

        // Simple in-memory cache so we don't hit disk on every single read/write
        // within one app session. Reloaded fresh from disk on first access.
        private static Dictionary<string, UndoEntry>? _cache;
        private static readonly object _lock = new();

        private class UndoEntry
        {
            public string? Value { get; set; }
            public DateTime RecordedAt { get; set; }
        }

        private static Dictionary<string, UndoEntry> LoadCache()
        {
            lock (_lock)
            {
                if (_cache != null)
                    return _cache;

                if (!File.Exists(ManifestPath))
                {
                    _cache = new Dictionary<string, UndoEntry>();
                    return _cache;
                }

                try
                {
                    var encryptedBytes = File.ReadAllBytes(ManifestPath);
                    var decryptedBytes = ProtectedData.Unprotect(
                        encryptedBytes, null, DataProtectionScope.CurrentUser);
                    var json = Encoding.UTF8.GetString(decryptedBytes);

                    _cache = JsonSerializer.Deserialize<Dictionary<string, UndoEntry>>(json)
                             ?? new Dictionary<string, UndoEntry>();
                }
                catch (Exception)
                {
                    // Corrupted or unreadable manifest (e.g. moved to a
                    // different Windows user account, where DPAPI can't
                    // decrypt it). Fail safe: start fresh rather than
                    // crashing the whole app. Tweaks will fall back to
                    // their hardcoded DefaultValue for Revert() until new
                    // entries are recorded.
                    _cache = new Dictionary<string, UndoEntry>();
                }

                return _cache;
            }
        }

        private static void SaveCache(Dictionary<string, UndoEntry> cache)
        {
            lock (_lock)
            {
                Directory.CreateDirectory(StorageDir);

                var json = JsonSerializer.Serialize(cache);
                var plainBytes = Encoding.UTF8.GetBytes(json);
                var encryptedBytes = ProtectedData.Protect(
                    plainBytes, null, DataProtectionScope.CurrentUser);

                File.WriteAllBytes(ManifestPath, encryptedBytes);
            }
        }

        /// <summary>
        /// Records the pre-change value for a tweak, right before Apply()
        /// modifies it. Overwrites any prior entry for the same tweak ID —
        /// we only need the most recent "known good" state to revert to.
        /// </summary>
        public static void RecordPreState(string tweakId, string? value)
        {
            var cache = LoadCache();
            cache[tweakId] = new UndoEntry
            {
                Value = value,
                RecordedAt = DateTime.Now
            };
            SaveCache(cache);
        }

        /// <summary>
        /// Retrieves the stored pre-change value for a tweak, or null if
        /// none exists (tweak was never applied, or the log was cleared).
        /// Callers should fall back to a hardcoded Windows default in that case.
        /// </summary>
        public static string? GetPreState(string tweakId)
        {
            var cache = LoadCache();
            return cache.TryGetValue(tweakId, out var entry) ? entry.Value : null;
        }

        /// <summary>
        /// Removes a tweak's stored pre-state after a successful revert,
        /// so a stale value isn't reused if the tweak is applied again later.
        /// </summary>
        public static void ClearPreState(string tweakId)
        {
            var cache = LoadCache();
            if (cache.Remove(tweakId))
                SaveCache(cache);
        }

        /// <summary>
        /// Returns all tweak IDs that currently have a recorded pre-state —
        /// used by the Activity Log / Undo History UI to show what's
        /// actually reversible right now.
        /// </summary>
        public static IReadOnlyList<string> GetAllRecordedTweakIds()
        {
            return new List<string>(LoadCache().Keys);
        }
    }
}