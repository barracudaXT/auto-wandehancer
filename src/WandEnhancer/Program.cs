using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WandEnhancer.Core.Extensions;
using WandEnhancer.Core.Services;
using WandEnhancer.Services;
using WandEnhancer.View.MainWindow;

namespace WandEnhancer
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            List<LogEntry> logEntries = new List<LogEntry>();
            if (args.Length > 0)
            {
                if (TryHandleCommandLine(args))
                    return;
            }

            var application = new App();
            application.InitializeComponent();
            application.MainWindow = new MainWindow();
            foreach (var logEntry in logEntries)
            {
                MainWindow.Instance.ViewModel.LogList.Add(logEntry);
            }
            application.Run();
        }

        private static bool TryHandleCommandLine(string[] args)
        {
            var setupLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WandEnhancer",
                "logs",
                "setup.log");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(setupLogPath));

                if (args.Length >= 2 && string.Equals(args[0], "--enable-autopatch", StringComparison.OrdinalIgnoreCase))
                {
                    var wandPath = args[1].Trim('"');
                    WriteSetupLog(setupLogPath, $"--enable-autopatch invoked with path: {wandPath}");

                    var payloadPath = PathExtensions.ResolveWeModPayloadPath(wandPath);
                    if (payloadPath == null)
                    {
                        var message = $"The selected folder is not a valid Wand directory:\n{wandPath}";
                        WriteSetupLog(setupLogPath, message);
                        MessageBox.Show(message, "WandEnhancer");
                        Environment.Exit(1);
                        return true;
                    }

                    var autoPatchPath = GetAutoPatchExePath();
                    WriteSetupLog(setupLogPath, $"AutoPatch executable: {autoPatchPath}");

                    // Store the root path (e.g. %LocalAppData%\WeMod) so future
                    // updates that create new app-* subfolders are still handled.
                    var shortcutRegistrar = new ShortcutRegistrar();
                    shortcutRegistrar.Register(wandPath, autoPatchPath);
                    WriteSetupLog(setupLogPath, $"Shortcut registration completed. IsRegistered: {shortcutRegistrar.IsRegistered()}");

                    new ScheduledTaskRegistrar().Create(wandPath, autoPatchPath);
                    WriteSetupLog(setupLogPath, "Scheduled task created.");

                    // Kill any existing watcher process so we don't end up with duplicate tray icons.
                    try
                    {
                        foreach (var proc in System.Diagnostics.Process.GetProcessesByName("WandEnhancer.AutoPatch"))
                        {
                            try { proc.Kill(); proc.WaitForExit(3000); } catch { }
                            proc.Dispose();
                        }
                        WriteSetupLog(setupLogPath, "Killed existing watcher process(es).");
                    }
                    catch (Exception killEx)
                    {
                        WriteSetupLog(setupLogPath, $"Failed to kill existing watcher: {killEx.Message}");
                    }

                    // Start the watcher immediately so it's running right now, not just at next logon.
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = autoPatchPath,
                            Arguments = $"--watch \"{wandPath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = Path.GetDirectoryName(autoPatchPath)
                        });
                        WriteSetupLog(setupLogPath, "Watcher process started.");
                    }
                    catch (Exception watchEx)
                    {
                        WriteSetupLog(setupLogPath, $"Failed to start watcher: {watchEx.Message}");
                        // Non-fatal — the scheduled task will start it at next logon.
                    }

                    MessageBox.Show("Auto-patch enabled successfully.", "WandEnhancer");
                    Environment.Exit(0);
                    return true;
                }

                if (string.Equals(args[0], "--disable-autopatch", StringComparison.OrdinalIgnoreCase))
                {
                    WriteSetupLog(setupLogPath, "--disable-autopatch invoked.");
                    new ShortcutRegistrar().Unregister();
                    new ScheduledTaskRegistrar().Delete();
                    MessageBox.Show("Auto-patch disabled successfully.", "WandEnhancer");
                    Environment.Exit(0);
                    return true;
                }
            }
            catch (Exception ex)
            {
                WriteSetupLog(setupLogPath, $"Command failed: {ex}");
                MessageBox.Show($"Command failed: {ex.Message}", "WandEnhancer");
                Environment.Exit(1);
                return true;
            }

            return false;
        }

        private static void WriteSetupLog(string path, string message)
        {
            try
            {
                var line = $"{DateTime.Now:O} {message}{Environment.NewLine}";
                File.AppendAllText(path, line);
            }
            catch
            {
                // Logging is best-effort.
            }
        }

        private static string GetAutoPatchExePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutoPatch", "WandEnhancer.AutoPatch.exe");
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            HandleCrash("Unobserved Task Exception", e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            HandleCrash("Unhandled Exception", exception ?? new Exception(e.ExceptionObject?.ToString() ?? "Unknown error"));
        }

        private static void HandleCrash(string category, Exception exception)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WandEnhancer", "logs");
            var logPath = Path.Combine(logDirectory, $"crash-{timestamp}.txt");

            try
            {
                Directory.CreateDirectory(logDirectory);
                var sb = new StringBuilder();
                sb.AppendLine($"WandEnhancer Crash Report");
                sb.AppendLine($"Timestamp: {DateTime.Now:O}");
                sb.AppendLine($"Category: {category}");
                sb.AppendLine($"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
                sb.AppendLine();
                sb.AppendLine($"Exception: {exception.GetType().FullName}");
                sb.AppendLine($"Message: {exception.Message}");
                sb.AppendLine();
                sb.AppendLine($"Stack Trace:");
                sb.AppendLine(exception.StackTrace);
                if (exception.InnerException != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Inner Exception: {exception.InnerException.GetType().FullName}");
                    sb.AppendLine($"Message: {exception.InnerException.Message}");
                    sb.AppendLine(exception.InnerException.StackTrace);
                }
                File.WriteAllText(logPath, sb.ToString());
            }
            catch
            {
                // Best-effort logging; don't crash in the crash handler.
            }

            MessageBox.Show(
                $"WandEnhancer encountered an unexpected error.\n\n" +
                $"Details have been saved to:\n{logPath}\n\n" +
                $"Please report this if the problem persists.",
                "WandEnhancer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            Environment.Exit(1);
        }
    }
}
