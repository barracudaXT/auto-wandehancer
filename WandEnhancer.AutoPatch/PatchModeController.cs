using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WandEnhancer.Core.Extensions;
using WandEnhancer.Core.Models;
using WandEnhancer.Core.Services;

namespace WandEnhancer.AutoPatch
{
    public class PatchModeController
    {
        private readonly ISettingsStore _settingsStore;
        private readonly IWeModLocator _locator;
        private readonly IProcessManager _processManager;
        private readonly IPatcher _patcher;
        private readonly ILogger _logger;
        private readonly INotificationService _notification;

        public PatchModeController(
            ISettingsStore settingsStore,
            IWeModLocator locator,
            IProcessManager processManager,
            IPatcher patcher,
            ILogger logger)
            : this(settingsStore, locator, processManager, patcher, logger, null)
        {
        }

        public PatchModeController(
            ISettingsStore settingsStore,
            IWeModLocator locator,
            IProcessManager processManager,
            IPatcher patcher,
            ILogger logger,
            INotificationService notification)
        {
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _locator = locator ?? throw new ArgumentNullException(nameof(locator));
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _patcher = patcher ?? throw new ArgumentNullException(nameof(patcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _notification = notification;
        }

        public async Task<bool> RunAsync(string configuredPath, IProgress<string> progress, ProgressWindow window = null)
        {
            try
            {
                progress?.Report("Locating Wand installation...");
                window?.SetStatus("Locating Wand installation...");

                var config = _settingsStore.Load();
                var path = configuredPath ?? config.Path;
                var info = await _locator.LocateAsync(path);

                if (info == null)
                {
                    progress?.Report("Failed to locate Wand installation.");
                    window?.ShowFailure("Could not locate Wand. Open WandEnhancer to set the path.");
                    return false;
                }

                // Store the root path so updates that create new app-* subfolders
                // are still picked up by the watcher and launcher.
                config.Path = info.RootPath ?? info.BasePath;
                _settingsStore.Save(config);

                if (PatchDecision.ShouldSkipPatch(
                    config.LastPatchedPayloadPath,
                    config.LastPatchedVersion,
                    info.BasePath,
                    info.Version,
                    PathExtensions.IsAlreadyPatched(info.BasePath)))
                {
                    progress?.Report("Wand is already patched at this version.");
                    window?.ShowSuccess("Wand is already patched at this version.");
                    return true;
                }

                var mutexAcquired = false;
                using (var patchMutex = new Mutex(false, @"Global\WandEnhancerAutoPatchLock"))
                {
                    try
                    {
                        try
                        {
                            mutexAcquired = patchMutex.WaitOne(0);
                        }
                        catch (AbandonedMutexException)
                        {
                            mutexAcquired = true;
                        }

                        if (!mutexAcquired)
                        {
                            progress?.Report("Waiting for another patch to finish...");
                            window?.SetStatus("Waiting for another patch to finish...");
                            try
                            {
                                mutexAcquired = patchMutex.WaitOne(TimeSpan.FromSeconds(60));
                            }
                            catch (AbandonedMutexException)
                            {
                                mutexAcquired = true;
                            }
                        }

                        if (!mutexAcquired)
                        {
                            _logger.Error("Timed out waiting for patch mutex.");
                            progress?.Report("Another patch is taking too long. Try again.");
                            window?.ShowFailure("Another patch is taking too long. Try again.");
                            return false;
                        }

                        config = _settingsStore.Load();
                        info = await _locator.LocateAsync(configuredPath ?? config.Path);
                        if (info != null && PatchDecision.ShouldSkipPatch(
                            config.LastPatchedPayloadPath,
                            config.LastPatchedVersion,
                            info.BasePath,
                            info.Version,
                            PathExtensions.IsAlreadyPatched(info.BasePath)))
                        {
                            progress?.Report("Wand is already patched at this version.");
                            window?.ShowSuccess("Wand is already patched at this version.");
                            return true;
                        }

                        if (info == null)
                        {
                            progress?.Report("Failed to locate Wand installation.");
                            window?.ShowFailure("Could not locate Wand. Open WandEnhancer to set the path.");
                            return false;
                        }

                        progress?.Report("Terminating Wand processes...");
                        window?.SetStatus("Terminating Wand processes...");
                        await _processManager.TerminateAllWandProcessesAsync(TimeSpan.FromSeconds(10));

                        progress?.Report("Patching Wand...");
                        window?.SetStatus("Patching Wand...");
                        await _patcher.PatchAsync(info, config);

                        config.LastPatchedPayloadPath = info.BasePath;
                        config.LastPatchedVersion = info.Version;
                        config.PatchingCompleted = true;
                        _settingsStore.Save(config);

                        try
                        {
                            var autoPatchExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                            var wandRootPath = info.RootPath ?? info.BasePath;
                            new ShortcutRegistrar().Register(wandRootPath, autoPatchExePath);
                            _logger.Info("Shortcuts re-registered after patch.");
                        }
                        catch (Exception scEx)
                        {
                            _logger.Error($"Failed to re-register shortcuts: {scEx.Message}");
                        }

                        progress?.Report("Patch completed.");
                        window?.ShowSuccess("Wand patched successfully.");
                        return true;
                    }
                    finally
                    {
                        if (mutexAcquired)
                            patchMutex.ReleaseMutex();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Auto-patch failed: {ex}");
                progress?.Report($"Patch failed: {ex.Message}");
                window?.ShowFailure($"Patch failed: {ex.Message}");
                _notification?.ShowError("WandEnhancer", $"Patch failed: {ex.Message}");
                return false;
            }
        }
    }
}
