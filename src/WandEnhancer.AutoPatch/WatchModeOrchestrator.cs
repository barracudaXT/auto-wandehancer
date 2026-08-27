using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WandEnhancer.Core.Services;

namespace WandEnhancer.AutoPatch
{
    class WatchModeOrchestrator
    {
        private readonly ISettingsStore _settingsStore;
        private readonly IWeModLocator _locator;
        private readonly IProcessManager _processManager;
        private readonly IPatcher _patcher;
        private readonly ILogger _logger;
        private readonly Action _openMainApplication;

        public WatchModeOrchestrator(
            ISettingsStore settingsStore,
            IWeModLocator locator,
            IProcessManager processManager,
            IPatcher patcher,
            ILogger logger,
            Action openMainApplication)
        {
            _settingsStore = settingsStore;
            _locator = locator;
            _processManager = processManager;
            _patcher = patcher;
            _logger = logger;
            _openMainApplication = openMainApplication;
        }

        public void Run(string path)
        {
            bool createdNew;
            using (var mutex = new Mutex(true, @"Global\WandEnhancerAutoPatchWatcher", out createdNew))
            {
                if (!createdNew)
                {
                    _logger.Info("Another watcher instance is already running. Exiting.");
                    return;
                }

                RunCore(path);
            }
        }

        private void RunCore(string path)
        {
            using (var cts = new CancellationTokenSource())
            {
                var tray = new TrayAgent();
                var patchController = new PatchModeController(_settingsStore, _locator, _processManager, _patcher, _logger, tray);
                var updateChecker = new UpdateChecker(_logger);
                var updateInstaller = new UpdateInstaller(_logger, tray);
                UpdateInfo pendingUpdate = null;
                Task watchTask = null;

                using (var watchController = new WatchModeController(patchController, _logger, tray))
                {
                    tray.PatchNowClicked += (s, e) =>
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                await patchController.RunAsync(path, null, null);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"Patch now failed: {ex}");
                            }
                        });
                    };
                    tray.OpenSettingsClicked += (s, e) => _openMainApplication();
                    tray.ExitClicked += (s, e) =>
                    {
                        cts.Cancel();
                        Application.Exit();
                    };
                    tray.WatcherEnabledChanged += (s, e) => watchController.Enabled = tray.WatcherEnabled;

                    tray.CheckForUpdatesClicked += (s, e) =>
                    {
                        _logger.Info("Manual update check requested.");
                        tray.ShowCheckingForUpdates();
                        Task.Run(async () =>
                        {
                            try
                            {
                                var info = await updateChecker.CheckForUpdateAsync();
                                if (info == null)
                                {
                                    _logger.Error("Manual update check: could not reach update server.");
                                    tray.ShowUpdateCheckFailed();
                                    MessageBox.Show("Could not check for updates. Try again later.",
                                        "WandEnhancer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return;
                                }
                                if (updateChecker.IsUpdateAvailable(info))
                                {
                                    _logger.Info($"Manual update check: update available ({info.TagName}).");
                                    pendingUpdate = info;
                                    tray.ShowUpdateAvailable(info.TagName);
                                }
                                else
                                {
                                    _logger.Info("Manual update check: up to date.");
                                    pendingUpdate = null;
                                    tray.ShowUpToDate();
                                    MessageBox.Show("You are running the latest version.",
                                        "WandEnhancer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"Manual update check failed: {ex}");
                                tray.ShowUpdateCheckFailed();
                                MessageBox.Show("Could not check for updates. Try again later.",
                                    "WandEnhancer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        });
                    };

                    tray.InstallUpdateClicked += (s, e) =>
                    {
                        var update = pendingUpdate;
                        if (update == null) return;
                        tray.ShowDownloading(0);
                        Task.Run(async () =>
                        {
                            var progress = new Progress<int>(pct =>
                            {
                                if (pct < 0)
                                    tray.ShowInstalling();
                                else
                                    tray.ShowDownloading(pct);
                            });
                            var installed = await updateInstaller.DownloadAndInstallAsync(update, cts.Token, progress);
                            if (installed)
                            {
                                cts.Cancel();
                                Application.Exit();
                            }
                            else
                            {
                                tray.ShowUpdateAvailable(update.TagName);
                            }
                        });
                    };

                    StartPeriodicUpdateCheck(updateChecker, tray, cts.Token, u => pendingUpdate = u);

                    watchTask = watchController.RunAsync(path, cts.Token);
                    Application.Run(tray);
                }

                try { watchTask?.Wait(TimeSpan.FromSeconds(5)); } catch { }
            }
        }

        private void StartPeriodicUpdateCheck(
            UpdateChecker checker,
            TrayAgent tray,
            CancellationToken token,
            Action<UpdateInfo> setPending)
        {
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), token);

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var info = await checker.CheckForUpdateAsync();
                            if (checker.IsUpdateAvailable(info))
                            {
                                setPending(info);
                                tray.ShowUpdateAvailable(info.TagName);
                                _logger.Info($"Update available: {info.TagName}");
                            }
                        }
                        catch (Exception ex) when (!(ex is OperationCanceledException))
                        {
                            _logger.Error($"Periodic update check failed: {ex}");
                        }

                        await Task.Delay(TimeSpan.FromHours(6), token);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }, token);
        }
    }
}
