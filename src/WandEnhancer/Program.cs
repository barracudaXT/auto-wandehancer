using System;
using System.Collections.Generic;
using System.IO;
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
            try
            {
                if (args.Length >= 2 && string.Equals(args[0], "--enable-autopatch", StringComparison.OrdinalIgnoreCase))
                {
                    var wandPath = args[1].Trim('"');
                    var payloadPath = PathExtensions.ResolveWeModPayloadPath(wandPath);
                    if (payloadPath == null)
                    {
                        MessageBox.Show($"The selected folder is not a valid Wand directory:\n{wandPath}", "WandEnhancer");
                        Environment.Exit(1);
                        return true;
                    }

                    var autoPatchPath = GetAutoPatchExePath();
                    // Store the root path (e.g. %LocalAppData%\WeMod) so future
                    // updates that create new app-* subfolders are still handled.
                    new ShortcutRegistrar().Register(wandPath, autoPatchPath);
                    new ScheduledTaskRegistrar().Create(wandPath, autoPatchPath);
                    MessageBox.Show("Auto-patch enabled successfully.", "WandEnhancer");
                    Environment.Exit(0);
                    return true;
                }

                if (string.Equals(args[0], "--disable-autopatch", StringComparison.OrdinalIgnoreCase))
                {
                    new ShortcutRegistrar().Unregister();
                    new ScheduledTaskRegistrar().Delete();
                    MessageBox.Show("Auto-patch disabled successfully.", "WandEnhancer");
                    Environment.Exit(0);
                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Command failed: {ex.Message}", "WandEnhancer");
                Environment.Exit(1);
                return true;
            }

            return false;
        }

        private static string GetAutoPatchExePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutoPatch", "WandEnhancer.AutoPatch.exe");
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString());
            Environment.Exit(1);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.ExceptionObject.ToString());
            Environment.Exit(1);
        }
    }
}
