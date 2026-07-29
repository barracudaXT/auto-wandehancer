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
            var locator = new WeModLocator(WandEnhancer.Core.Extensions.PathExtensions.CheckWeModPath);
            var processManager = new ProcessManager(logger);
            var patcher = new Patcher(patchLogger);
            var patchController = new PatchModeController(settingsStore, locator, processManager, patcher, logger);

            if (string.IsNullOrEmpty(arguments.Mode))
            {
                MessageBox.Show("Usage: WandEnhancer.AutoPatch.exe --patch [path] | --launch [path] [wand args] | --watch [path]", "WandEnhancer Auto-Patch");
                return;
            }

            switch (arguments.Mode)
            {
                case "patch":
                    RunPatchMode(patchController, arguments.WeModPath, logger).GetAwaiter().GetResult();
                    break;
                case "launch":
                    RunLaunchMode(patchController, arguments.WeModPath, GetWandArgs(args), logger).GetAwaiter().GetResult();
                    break;
                case "watch":
                    RunWatchMode(patchController, arguments.WeModPath, logger);
                    break;
            }
        }

        private static async Task RunPatchMode(PatchModeController controller, string path, ILogger logger)
        {
            using (var window = new ProgressWindow())
            using (var retrySignal = new SemaphoreSlim(0, 1))
            {
                var retryLock = new object();
                var retryRequested = false;
                var stop = false;
                var openMainInvoked = false;
                var doneTcs = new TaskCompletionSource<object>();

                void WakeRetrySignal()
                {
                    try { retrySignal.Release(); } catch (SemaphoreFullException) { }
                }

                window.FormClosed += (s, e) =>
                {
                    stop = true;
                    doneTcs.TrySetResult(null);
                    WakeRetrySignal();
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
                    WakeRetrySignal();
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

                _ = Task.Run(async () =>
                {
                    while (!stop)
                    {
                        await retrySignal.WaitAsync();

                        lock (retryLock)
                        {
                            retryRequested = false;
                        }

                        try
                        {
                            var success = await controller.RunAsync(path, new Progress<string>(m => window.SetStatus(m)), window);
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

                window.ShowDialog();
                await doneTcs.Task;
            }
        }

        private static async Task RunLaunchMode(PatchModeController controller, string path, string[] wandArgs, ILogger logger)
        {
            using (var window = new ProgressWindow())
            {
                var launchController = new LaunchModeController(controller, logger);
                var t = launchController.RunAsync(path, wandArgs, window);
                window.ShowDialog();
                await t;
            }
        }

        private static void RunWatchMode(PatchModeController controller, string path, ILogger logger)
        {
            using (var cts = new CancellationTokenSource())
            using (var watchController = new WatchModeController(controller, logger))
            {
                var tray = new TrayAgent();

                tray.PatchNowClicked += async (s, e) =>
                {
                    try
                    {
                        await controller.RunAsync(path, null, null);
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Patch now failed: {ex}");
                    }
                };
                tray.OpenSettingsClicked += (s, e) => OpenMainApplication();
                tray.ExitClicked += (s, e) =>
                {
                    cts.Cancel();
                    Application.Exit();
                };
                tray.WatcherEnabledChanged += (s, e) => watchController.Enabled = tray.WatcherEnabled;

                var task = watchController.RunAsync(path, cts.Token);
                Application.Run(tray);
                try { task.Wait(TimeSpan.FromSeconds(5)); } catch { }
            }
        }

        private static void OpenMainApplication()
        {
            var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WandEnhancer.exe");
            if (File.Exists(exePath))
            {
                Process.Start(exePath);
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
