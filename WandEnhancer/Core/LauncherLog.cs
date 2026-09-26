using System;
using System.IO;

namespace WandEnhancer.Core
{
    /// <summary>
    /// Append-only log written to a location the process can actually write.
    /// Diagnostics failures are swallowed so they never prevent Wand from launching.
    /// </summary>
    internal static class LauncherLog
    {
        public const string FileName = "launcher.log";
        public const string PreviousFileName = "launcher.prev.log";
        private const long MaxBytes = 512 * 1024;

        private static string _path;

        /// <summary>Directory the log resolved to, or null before a successful Open.</summary>
        public static string LogDirectory { get; private set; }

        public static bool IsOpen => _path != null;

        /// <summary>
        /// Test seam: when set, the log is written here instead of resolving a directory.
        /// ponytail: one static, not an injected sink. Tests run single-threaded.
        /// </summary>
        internal static string DirectoryOverride { get; set; }

        public static void Open(string launcherDirectory, string header)
        {
            try
            {
                foreach (var candidate in BuildCandidates(launcherDirectory))
                {
                    if (candidate != null && TryOpenIn(candidate, header))
                    {
                        return;
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException ||
                                      e is ArgumentException || e is NotSupportedException ||
                                      e is System.Security.SecurityException)
            {
                // Diagnostics must never prevent the launch. Building the candidate list
                // resolves paths, which can throw before TryOpenIn's own guard is reached -
                // and no caller guards Open().
            }

            _path = null;
            LogDirectory = null;
        }

        private static string[] BuildCandidates(string launcherDirectory)
        {
            if (DirectoryOverride != null)
            {
                // The seam is exclusive on purpose: a test that pins the directory must
                // never fall through into the user's real log directory.
                return new[] { DirectoryOverride };
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new[]
            {
                string.IsNullOrEmpty(localAppData)
                    ? null
                    : Path.Combine(localAppData, "WandEnhancer", "logs"),
                string.IsNullOrEmpty(launcherDirectory) ? null : launcherDirectory,
                Path.GetTempPath()
            };
        }

        public static void Close()
        {
            _path = null;
            LogDirectory = null;
        }

        /// <summary>
        /// Adopts a directory only after a real append to the actual log file succeeds.
        /// A probe file is not enough: it can succeed while launcher.log itself is
        /// read-only, a directory, or append-denied - and Write() swallows that failure,
        /// leaving IsOpen true over a dead sink with no fallback tried.
        /// </summary>
        private static bool TryOpenIn(string directory, string header)
        {
            try
            {
                Directory.CreateDirectory(directory);

                string path = Path.Combine(directory, FileName);
                var file = new FileInfo(path);
                // Rotated whole. One previous generation is kept.
                if (file.Exists && file.Length > MaxBytes)
                {
                    string previous = Path.Combine(directory, PreviousFileName);
                    File.Delete(previous);
                    file.MoveTo(previous);
                }

                File.AppendAllText(path,
                    $"{DateTime.Now:HH:mm:ss.fff} [INFO] === {DateTime.Now:yyyy-MM-dd} {header}{Environment.NewLine}");

                _path = path;
                LogDirectory = directory;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException ||
                                      e is ArgumentException || e is NotSupportedException)
            {
                return false;
            }
        }

        public static void Write(string message, ELogType type)
        {
            if (_path == null)
            {
                return;
            }

            try
            {
                // ponytail: launcher and watcher may append concurrently; a line can
                // interleave or be lost. Accepted for a diagnostic log. Add
                // cross-process locking only if lost lines are observed in practice.
                File.AppendAllText(_path,
                    $"{DateTime.Now:HH:mm:ss.fff} [{type.ToString().ToUpperInvariant()}] {message}{Environment.NewLine}");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // A log line lost to a locked or full disk must not abort the launch.
            }
        }
    }
}
