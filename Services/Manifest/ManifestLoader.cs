using System;
using System.IO;
using System.Text.Json;

namespace ZeroBloat.Services.Manifest
{
    /// <summary>
    /// Loads tweaks.json at startup. For now this only reads the manifest
    /// bundled with the app install; the remote, checksum-verified fetch
    /// (GitHub Releases API) gets layered on top of this later without
    /// changing how the rest of the app consumes the manifest.
    /// </summary>
    public static class ManifestLoader
    {
        private const string MinSupportedManifestVersion = "1.0.0";

        private static TweakManifest? _cached;

        /// <summary>
        /// Path to the bundled default manifest, shipped alongside the exe.
        /// Set "Copy to Output Directory" = "Copy if newer" on tweaks.json
        /// in its file properties so this path resolves correctly at runtime.
        /// </summary>
        private static string BundledManifestPath =>
            Path.Combine(AppContext.BaseDirectory, "tweaks.json");

        /// <summary>
        /// Path to a locally cached, possibly newer manifest fetched from
        /// GitHub Releases. Checked first; falls back to the bundled copy
        /// if it doesn't exist yet (first run, before any update check).
        /// </summary>
        private static string CachedManifestPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZeroBloat", "tweaks_cached.json");

        public static TweakManifest Load(bool forceReload = false)
        {
            if (_cached != null && !forceReload)
                return _cached;

            string pathToUse = File.Exists(CachedManifestPath)
                ? CachedManifestPath
                : BundledManifestPath;

            if (!File.Exists(pathToUse))
            {
                throw new FileNotFoundException(
                    $"tweaks.json not found at either cached path ({CachedManifestPath}) " +
                    $"or bundled path ({BundledManifestPath}). Ensure tweaks.json is set to " +
                    "'Copy to Output Directory' in its file properties.");
            }

            var json = File.ReadAllText(pathToUse);
            var manifest = JsonSerializer.Deserialize<TweakManifest>(json)
                ?? throw new InvalidDataException($"tweaks.json at {pathToUse} deserialized to null.");

            if (!IsVersionSupported(manifest.ManifestVersion))
            {
                throw new InvalidOperationException(
                    $"tweaks.json version {manifest.ManifestVersion} is older than the minimum " +
                    $"supported version {MinSupportedManifestVersion}. This should not normally " +
                    "happen with the bundled manifest — check for a corrupted cached copy.");
            }

            _cached = manifest;
            return manifest;
        }

        private static bool IsVersionSupported(string version)
        {
            // Simple major.minor.patch string comparison is sufficient here
            // since we only need "at least this version," not full semver
            // range logic.
            return string.CompareOrdinal(version, MinSupportedManifestVersion) >= 0;
        }

        /// <summary>
        /// Called after a successful, checksum-verified remote manifest fetch.
        /// Overwrites the cached copy and forces the next Load() to re-read it.
        /// </summary>
        public static void SaveAsCached(string rawJson)
        {
            var dir = Path.GetDirectoryName(CachedManifestPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(CachedManifestPath, rawJson);
            _cached = null; // force reload next Load() call
        }
    }
}