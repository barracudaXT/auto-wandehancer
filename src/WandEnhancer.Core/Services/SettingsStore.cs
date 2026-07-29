using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Services
{
    public class SettingsStore : ISettingsStore, IDisposable
    {
        private readonly string _filePath;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private bool _disposed;

        public SettingsStore(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lock.Dispose();
        }

        public PatchConfig Load()
        {
            _lock.EnterReadLock();
            try
            {
                if (!File.Exists(_filePath))
                {
                    Debug.WriteLine($"[SettingsStore] Settings file not found at '{_filePath}'. Falling back to defaults.");
                    return new PatchConfig();
                }

                var json = File.ReadAllText(_filePath);
                var config = JsonConvert.DeserializeObject<PatchConfig>(json);
                if (config == null)
                {
                    Debug.WriteLine($"[SettingsStore] Settings file at '{_filePath}' is empty or invalid. Falling back to defaults.");
                    return new PatchConfig();
                }

                return config;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsStore] Failed to load settings from '{_filePath}': {ex.Message}. Falling back to defaults.");
                return new PatchConfig();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Save(PatchConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _lock.EnterWriteLock();
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

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
            finally
            {
                _lock.ExitWriteLock();
            }
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
