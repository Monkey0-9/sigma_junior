using System;
using System.IO;

namespace Hft.Infra
{
    /// <summary>
    /// Institutional Filesystem Bootstrap.
    /// Ensures deterministic, idempotent creation of required directories.
    /// Aligns with BlackRock/JPM/Jane Street operational standards.
    /// </summary>
    public static class FilesystemBootstrap
    {
        private static readonly object _lock = new();
        private static bool _initialized;

        /// <summary>
        /// Ensures all required directories exist.
        /// Safe to call multiple times. Fails fast on permission errors.
        /// </summary>
        public static void EnsureDirectories(string baseDir)
        {
            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    // Audit logs (HMAC-signed binary + JSONL)
                    Directory.CreateDirectory(Path.Combine(baseDir, "data", "audit"));

                    // Metrics export
                    Directory.CreateDirectory(Path.Combine(baseDir, "data", "metrics"));

                    // Replay data
                    Directory.CreateDirectory(Path.Combine(baseDir, "data", "replay"));

                    // Application logs
                    Directory.CreateDirectory(Path.Combine(baseDir, "logs"));

                    _initialized = true;
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new InvalidOperationException(
                        $"Filesystem bootstrap failed: insufficient permissions for {baseDir}", ex);
                }
                catch (IOException ex)
                {
                    throw new InvalidOperationException(
                        $"Filesystem bootstrap failed: I/O error creating directories in {baseDir}", ex);
                }
            }
        }

        /// <summary>
        /// Resets initialization state (for testing only).
        /// </summary>
        internal static void Reset() => _initialized = false;
    }
}

