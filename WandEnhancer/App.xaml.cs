using System;
using System.Threading.Tasks;
using System.Windows;
using WandEnhancer.Core;
using WandEnhancer.Core.Services;
using WandEnhancer.View.MainWindow;
using MessageBox = System.Windows.Forms.MessageBox;

namespace WandEnhancer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        public App()
        {
            // Without this, an exception on the WPF UI thread is an unhandled crash
            // with nothing written to any log. Attached in the constructor so it is
            // in place before any window or view model runs.
            DispatcherUnhandledException += (sender, e) =>
            {
                Program.LogFatal(e.Exception);
                System.Windows.MessageBox.Show(
                    e.Exception?.Message ?? "Unknown error",
                    Constants.RepoName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                e.Handled = true;
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            LocalizationManager.Initialize();
            this.MainWindow.Show();
        }

        public new static void Shutdown()
        {
            Current.Dispatcher.Invoke(() => Current.Shutdown());
        }
    }
}