using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WandEnhancer.Core.Services;

namespace WandEnhancer.AutoPatch
{
    public class WatchModeController : IDisposable
    {
        private readonly PatchModeController _patchController;
        private readonly ILogger _logger;
        private readonly INotificationService _notification;
        private readonly SemaphoreSlim _patchSemaphore = new SemaphoreSlim(1, 1);
        private readonly object _watcherLock = new object();
        private readonly TimeSpan _debounceInterval = TimeSpan.FromSeconds(5);
        private FileSystemWatcher _watcher;
        private bool _pendingRecheck;
        private bool _enabled = true;
        private CancellationTokenSource _lifetimeCts;
        private Task _inFlightPatchTask;

        public bool Enabled
        {
            get { lock (_watcherLock) return _enabled; }
            set
            {
                lock (_watcherLock)
                {
                    _enabled = value;
                    if (_watcher != null)
                    {
                        _watcher.EnableRaisingEvents = _enabled;
                    }
                }
                _logger.Info($"Watcher {(_enabled ? "enabled" : "paused")}.");
            }
        }

        public WatchModeController(PatchModeController patchController, ILogger logger)
            : this(patchController, logger, new NotificationService())
        {
        }

        public WatchModeController(PatchModeController patchController, ILogger logger, INotificationService notification)
        {
            _patchController = patchController ?? throw new ArgumentNullException(nameof(patchController));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _notification = notification ?? throw new ArgumentNullException(nameof(notification));
        }

        public void Dispose()
        {
            _lifetimeCts?.Cancel();

            var task = Interlocked.Exchange(ref _inFlightPatchTask, null);
            if (task != null)
            {
                try
                {
                    if (!task.Wait(TimeSpan.FromSeconds(10)))
                        _logger.Error("Dispose timed out waiting for in-flight patch task.");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Dispose waiting for in-flight patch task failed: {ex}");
                }
            }

            _lifetimeCts?.Dispose();
            _patchSemaphore?.Dispose();
        }

        public Task RunAsync(string configuredPath, CancellationToken token)
        {
            _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            return Task.Run(async () =>
            {
                var locator = new WeModLocator(WandEnhancer.Core.Extensions.PathExtensions.CheckWeModPath, allowManualFallback: false);
                var info = await locator.LocateAsync(configuredPath);
                if (info == null)
                {
                    _logger.Error("Watcher could not locate Wand installation.");
                    return;
                }

                // Watch the root folder so WeMod updates that create a new app-*
                // subfolder are detected.
                var watchPath = info.RootPath ?? info.BasePath;
                _watcher = new FileSystemWatcher(watchPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
                };

                _watcher.Changed += OnChanged;
                _watcher.Created += OnChanged;
                _watcher.Deleted += OnChanged;
                _watcher.Renamed += OnChanged;

                lock (_watcherLock)
                {
                    _watcher.EnableRaisingEvents = _enabled;
                }

                _logger.Info($"Watcher started for {watchPath}");

                try
                {
                    await Task.Delay(Timeout.Infinite, _lifetimeCts.Token);
                }
                catch (TaskCanceledException)
                {
                    // expected
                }
                finally
                {
                    _lifetimeCts?.Cancel();
                    lock (_watcherLock)
                    {
                        if (_watcher != null)
                        {
                            _watcher.EnableRaisingEvents = false;
                            _watcher.Changed -= OnChanged;
                            _watcher.Created -= OnChanged;
                            _watcher.Deleted -= OnChanged;
                            _watcher.Renamed -= OnChanged;
                            _watcher.Dispose();
                            _watcher = null;
                        }
                    }
                }
            }, token);
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            lock (_watcherLock)
            {
                _pendingRecheck = true;
            }
            var task = HandlePendingAsync(_lifetimeCts.Token);
            Interlocked.Exchange(ref _inFlightPatchTask, task);
            _ = task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.Error($"In-flight patch task faulted: {t.Exception}");
                bool needRecheck;
                lock (_watcherLock) { needRecheck = _pendingRecheck; }
                if (needRecheck && !t.IsFaulted)
                {
                    var followUp = HandlePendingAsync(_lifetimeCts.Token);
                    Interlocked.Exchange(ref _inFlightPatchTask, followUp);
                    _ = followUp.ContinueWith(ft =>
                    {
                        if (ft.IsFaulted)
                            _logger.Error($"Follow-up patch task faulted: {ft.Exception}");
                        Interlocked.CompareExchange(ref _inFlightPatchTask, null, followUp);
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                Interlocked.CompareExchange(ref _inFlightPatchTask, null, task);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private async Task HandlePendingAsync(CancellationToken token)
        {
            if (!await _patchSemaphore.WaitAsync(0))
            {
                // A patch is already running. The latch stays set so the in-flight
                // task's completion handler re-invokes us after it finishes.
                return;
            }

            try
            {
                // De-dup burst: give the updater time to finish writing files.
                await Task.Delay(_debounceInterval, token).ConfigureAwait(false);

                // Loop: keep patching until no new changes arrived during the last patch.
                while (true)
                {
                    lock (_watcherLock) { _pendingRecheck = false; }

                    var success = await _patchController.RunAsync(null, null, null).ConfigureAwait(false);
                    if (success)
                        _notification.ShowInfo("WandEnhancer", "Wand was re-patched after an update.");
                    else
                        _notification.ShowWarning("WandEnhancer", "Auto-patch failed. Open WandEnhancer for details.");

                    lock (_watcherLock) { if (!_pendingRecheck) return; }
                }
            }
            catch (OperationCanceledException)
            {
                // expected when watcher is stopped
            }
            catch (Exception ex)
            {
                _logger.Error($"Watcher patch handler failed: {ex}");
                _notification.ShowError("WandEnhancer", $"Auto-patch error: {ex.Message}");
            }
            finally
            {
                _patchSemaphore.Release();
            }
        }
    }
}
