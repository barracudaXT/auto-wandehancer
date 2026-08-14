using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Services
{
    public class SettingsStore : ISettingsStore, IDisposable
    {
        private readonly string _filePath;
        private readonly string _lockFilePath;
        private readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(30);
        private bool _disposed;

        public SettingsStore(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _lockFilePath = filePath + ".lock";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }

        public PatchConfig Load()
        {
            EnsureDirectoryExists(_lockFilePath);
            using (var fileLock = AcquireCrossProcessLock(_lockFilePath))
            {
                return LoadUnlocked();
            }
        }

        public void Save(PatchConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            EnsureDirectoryExists(_lockFilePath);
            using (var fileLock = AcquireCrossProcessLock(_lockFilePath))
            {
                SaveUnlocked(config);
            }
        }

        private PatchConfig LoadUnlocked()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    Debug.WriteLine($"[SettingsStore] Settings file not found at '{_filePath}'. Falling back to defaults.");
                    return new PatchConfig();
                }

                var json = File.ReadAllText(_filePath);
                var serializerSettings = new JsonSerializerSettings
                {
                    // Ensure deserialized values replace pre-initialized collections instead of
                    // being merged with them (important for PatchTypes defaults).
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                };
                var config = JsonConvert.DeserializeObject<PatchConfig>(json, serializerSettings);
                if (config == null)
                {
                    Debug.WriteLine($"[SettingsStore] Settings file at '{_filePath}' is empty or invalid. Falling back to defaults.");
                    return new PatchConfig();
                }

                // Migration: older/fresh configs may have been saved with an empty patch list,
                // which makes auto-patch silently do nothing. Apply the default patch set once.
                if (config.PatchTypes == null || config.PatchTypes.Count == 0)
                {
                    config.PatchTypes = new HashSet<EPatchType>
                    {
                        EPatchType.ActivatePro,
                        EPatchType.RemoteWebPanelPreview
                    };
                    try
                    {
                        SaveUnlocked(config);
                    }
                    catch (Exception saveEx)
                    {
                        Debug.WriteLine($"[SettingsStore] Applied default patch types but failed to persist them: {saveEx.Message}");
                    }
                }

                return config;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsStore] Failed to load settings from '{_filePath}': {ex.Message}. Falling back to defaults.");
                return new PatchConfig();
            }
        }

        private void SaveUnlocked(PatchConfig config)
        {
            EnsureDirectoryExists(_filePath);

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            var tempPath = _filePath + ".tmp";
            try
            {
                File.WriteAllText(tempPath, json);
                if (File.Exists(_filePath))
                {
                    File.Replace(tempPath, _filePath, _filePath + ".backup", ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, _filePath);
                }
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        private void EnsureDirectoryExists(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private FileStream AcquireCrossProcessLock(string lockPath)
        {
            const int retryMs = 50;
            var stopwatch = Stopwatch.StartNew();
            Exception lastEx = null;

            while (stopwatch.Elapsed < _lockTimeout)
            {
                try
                {
                    // Use FileShare.Read to allow other processes to observe the lock file
                    // while we hold it, but deny them the exclusive access needed to lock it.
                    var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.DeleteOnClose);
                    try
                    {
                        stream.Lock(0, long.MaxValue);
                        return stream;
                    }
                    catch
                    {
                        stream.Dispose();
                        throw;
                    }
                }
                catch (IOException ex)
                {
                    lastEx = ex;
                    Thread.Sleep(retryMs);
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastEx = ex;
                    Thread.Sleep(retryMs);
                }
            }

            throw new TimeoutException($"Unable to acquire cross-process lock on '{lockPath}' within {_lockTimeout.TotalSeconds}s.", lastEx);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort cleanup; original exception is more important.
            }
        }
    }
}
