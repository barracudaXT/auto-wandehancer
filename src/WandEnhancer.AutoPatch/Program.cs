using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WandEnhancer.Core.Models;
using WandEnhancer.Core.Services;

namespace WandEnhancer.AutoPatch
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var arguments = AutoPatchArguments.Parse(args);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var settingsPath = Path.Combine(appData, "WandEnhancer", "appsettings.json");
            var logDirectory = Path.Combine(appData, "WandEnhancer", "logs");

            var logger = new FileLogger(logDirectory);
            var patchLogger = new Action<string, ELogType>((msg, type) => logger.Info(msg));
            var settingsStore = new SettingsStore(settingsPath);
            var locator = new WeModLocator(WandEnhancer.Core.Extensions.PathExtensions.CheckWeModPath, allowManualFallback: false);
            var processManager = new ProcessManager(logger);
            var patcher = new Patcher(patchLogger);

            if (string.IsNullOrEmpty(arguments.Mode))
            {
                MessageBox.Show("Usage: WandEnhancer.AutoPatch.exe --patch [path] | --launch [path] [wand args] | --watch [path]", "WandEnhancer Auto-Patch");
                return;
            }

            switch (arguments.Mode)
            {
                case "patch":
                    RunPatchMode(settingsStore, locator, processManager, patcher, logger, arguments.WeModPath).GetAwaiter().GetResult();
                    break;
                case "launch":
                    RunLaunchMode(settingsStore, locator, processManager, patcher, logger, arguments.WeModPath, GetWandArgs(args)).GetAwaiter().GetResult();
                    break;
                case "watch":
                    RunWatchMode(settingsStore, locator, processManager, patcher, logger, arguments.WeModPath);
                    break;
            }
        }

        private static async Task RunPatchMode(ISettingsStore settingsStore, IWeModLocator locator, IProcessManager processManager, IPatcher patcher, ILogger logger, string path)
        {
            using (var notification = new NotificationService())
            using (var window = new ProgressWindow())
            {
                // Ensure the window handle is created before the background task
                // tries to Invoke status updates onto the UI thread.
                var dummyHandle = window.Handle;

                using (var retrySignal = new SemaphoreSlim(0, 1))
                {
                    var patchController = new PatchModeController(settingsStore, locator, processManager, patcher, logger, notification);
                    var retryLock = new object();
                    var retryRequested = false;
                    var stop = false;
                    var openMainInvoked = false;
                    var attemptCount = 0;
                    var doneTcs = new TaskCompletionSource<object>();

                    Action wakeRetrySignal = () =>
                    {
                        try { retrySignal.Release(); } catch (SemaphoreFullException) { }
                    };

                    window.FormClosed += (s, e) =>
                    {
                        stop = true;
                        doneTcs.TrySetResult(null);
                        wakeRetrySignal();
                    };

                    window.OpenMainRequested += (s, e) =>
                    {
                        if (openMainInvoked)
                            return;
                        openMainInvoked = true;

                        try
                        {
                            OpenMainApplication();
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Open main application failed: {ex}");
                        }
                        stop = true;
                        doneTcs.TrySetResult(null);
                        window.SafeClose();
                        wakeRetrySignal();
                    };

                    window.RetryRequested += (s, e) =>
                    {
                        lock (retryLock)
                        {
                            retryRequested = true;
                            if (retrySignal.CurrentCount == 0)
                                retrySignal.Release();
                        }
                    };

                    retrySignal.Release(); // initial attempt

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Run(async () =>
                    {
                        while (!stop)
                        {
                            await retrySignal.WaitAsync();

                            lock (retryLock)
                            {
                                retryRequested = false;
                            }

                            attemptCount++;
                            if (attemptCount > 3)
                            {
                                logger.Error("Maximum auto-patch retry attempts exceeded.");
                                break;
                            }

                            try
                            {
                                var success = await patchController.RunAsync(path, new Progress<string>(m => window.SetStatus(m)), window);
                                if (success || stop)
                                    break;
                            }
                            catch (Exception ex)
                            {
                                logger.Error($"Patch attempt failed: {ex}");
                            }

                            if (stop)
                                break;

                            lock (retryLock)
                            {
                                if (retryRequested)
                                {
                                    retryRequested = false;
                                    continue;
                                }
                            }

                            // No retry requested during the attempt; wait for the next retry signal.
                        }

                        doneTcs.TrySetResult(null);
                    });
#pragma warning restore CS4014

                    window.ShowDialog();
                    await doneTcs.Task;
                }
            }
        }

        private static async Task RunLaunchMode(ISettingsStore settingsStore, IWeModLocator locator, IProcessManager processManager, IPatcher patcher, ILogger logger, string path, string[] wandArgs)
        {
            using (var notification = new NotificationService())
            using (var window = new ProgressWindow())
            {
                // Ensure the window handle is created before the background task
                // tries to Invoke status updates onto the UI thread.
                var dummyHandle = window.Handle;

                var patchController = new PatchModeController(settingsStore, locator, processManager, patcher, logger, notification);
                var launchController = new LaunchModeController(patchController, logger);

                window.RetryRequested += (s, e) =>
                {
                    window.SetStatus("Retrying patch...");
                    window.HideFailureButtons();
                    _ = Task.Run(async () => await launchController.RunAsync(path, wandArgs, window));
                };

                window.OpenMainRequested += (s, e) =>
                {
                    try
                    {
                        OpenMainApplication();
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Open main application failed: {ex}");
                    }
                    window.SafeClose();
                };

                var t = launchController.RunAsync(path, wandArgs, window);
                window.ShowDialog();
                await t;
            }
        }

        private static void RunWatchMode(ISettingsStore settingsStore, IWeModLocator locator, IProcessManager processManager, IPatcher patcher, ILogger logger, string path)
        {
            bool createdNew;
            using (var mutex = new Mutex(true, @"Global\WandEnhancerAutoPatchWatcher", out createdNew))
            {
                if (!createdNew)
                {
                    logger.Info("Another watcher instance is already running. Exiting.");
                    return;
                }

            using (var cts = new CancellationTokenSource())
            {
                var tray = new TrayAgent();
                var patchController = new PatchModeController(settingsStore, locator, processManager, patcher, logger, tray);
                var updateChecker = new UpdateChecker(logger);
                var updateInstaller = new UpdateInstaller(logger, tray);
                UpdateInfo pendingUpdate = null;
                Task watchTask = null;

                using (var watchController = new WatchModeController(patchController, logger, tray))
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
                                logger.Error($"Patch now failed: {ex}");
                            }
                        });
                    };
                    tray.OpenSettingsClicked += (s, e) => OpenMainApplication();
                    tray.ExitClicked += (s, e) =>
                    {
                        cts.Cancel();
                        Application.Exit();
                    };
                    tray.WatcherEnabledChanged += (s, e) => watchController.Enabled = tray.WatcherEnabled;

                    tray.CheckForUpdatesClicked += (s, e) =>
                    {
                        tray.ShowCheckingForUpdates();
                        Task.Run(async () =>
                        {
                            try
                            {
                                var info = await updateChecker.CheckForUpdateAsync();
                                if (info == null)
                                {
                                    tray.ShowUpdateCheckFailed();
                                    tray.ShowError("WandEnhancer", "Could not check for updates. Try again later.");
                                    return;
                                }
                                if (updateChecker.IsUpdateAvailable(info))
                                {
                                    pendingUpdate = info;
                                    tray.ShowUpdateAvailable(info.TagName);
                                }
                                else
                                {
                                    pendingUpdate = null;
                                    tray.ShowUpToDate();
                                    tray.ShowInfo("WandEnhancer", "You are running the latest version.");
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Error($"Manual update check failed: {ex}");
                                tray.ShowUpdateCheckFailed();
                                tray.ShowError("WandEnhancer", "Could not check for updates. Try again later.");
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

                    StartPeriodicUpdateCheck(updateChecker, tray, logger, cts.Token, u => pendingUpdate = u);

                    watchTask = watchController.RunAsync(path, cts.Token);
                    Application.Run(tray);
                }

                try { watchTask?.Wait(TimeSpan.FromSeconds(5)); } catch { }
            }
            } // mutex
        }

        private static void StartPeriodicUpdateCheck(
            UpdateChecker checker,
            TrayAgent tray,
            ILogger logger,
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
                                logger.Info($"Update available: {info.TagName}");
                            }
                        }
                        catch (Exception ex) when (!(ex is OperationCanceledException))
                        {
                            logger.Error($"Periodic update check failed: {ex}");
                        }

                        await Task.Delay(TimeSpan.FromHours(6), token);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }, token);
        }

        private static void OpenMainApplication()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "WandEnhancer.exe"),
                Path.Combine(Directory.GetParent(baseDir).FullName, "WandEnhancer.exe")
            };

            foreach (var exePath in candidates)
            {
                if (File.Exists(exePath))
                {
                    try
                    {
                        Process.Start(exePath);
                    }
                    catch (Exception)
                    {
                        // Best effort.
                    }
                    return;
                }
            }
        }

        private static string[] GetWandArgs(string[] args)
        {
            var list = new List<string>();
            bool foundPath = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--")) continue;
                if (!foundPath)
                {
                    foundPath = true;
                    continue;
                }
                list.Add(args[i]);
            }
            return list.ToArray();
        }
    }
}
