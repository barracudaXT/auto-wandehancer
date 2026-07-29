using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using WandEnhancer.Core.Extensions;
using WandEnhancer.Core.Services;
using WandEnhancer.ReactiveUICore;
using WandEnhancer.Services;

namespace WandEnhancer.View.AutoPatch
{
    public class AutoPatchSetupVm : ObservableObject
    {
        private readonly ISettingsStore _settingsStore;
        private string _statusMessage;
        private string _weModPath;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string WeModPath
        {
            get => _weModPath;
            set
            {
                if (SetProperty(ref _weModPath, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public AsyncRelayCommand EnableCommand { get; }
        public AsyncRelayCommand DisableCommand { get; }
        public RelayCommand PickPathCommand { get; }

        public AutoPatchSetupVm(ISettingsStore settingsStore)
        {
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            var config = _settingsStore.Load();
            WeModPath = config.Path;

            if (string.IsNullOrWhiteSpace(WeModPath) || !PathExtensions.CheckWeModPath(WeModPath))
            {
                try
                {
                    var locator = new WeModLocator(PathExtensions.CheckWeModPath, allowManualFallback: false);
                    var info = locator.LocateAsync().GetAwaiter().GetResult();
                    if (info != null)
                        WeModPath = info.BasePath;
                }
                catch
                {
                    // ignored
                }
            }

            EnableCommand = new AsyncRelayCommand(async _ => await OnEnableAsync(), _ => CanEnable());
            DisableCommand = new AsyncRelayCommand(async _ => await OnDisableAsync());
            PickPathCommand = new RelayCommand(_ => OnPickPath());
        }

        private bool CanEnable()
        {
            return !string.IsNullOrWhiteSpace(WeModPath) && PathExtensions.CheckWeModPath(WeModPath);
        }

        private void OnPickPath()
        {
            try
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select the Wand directory";
                    dialog.ShowNewFolderButton = false;

                    if (!string.IsNullOrWhiteSpace(WeModPath) && Directory.Exists(WeModPath))
                        dialog.SelectedPath = WeModPath;

                    if (dialog.ShowDialog() != DialogResult.OK)
                        return;

                    if (!PathExtensions.CheckWeModPath(dialog.SelectedPath))
                    {
                        StatusMessage = "The selected folder is not a valid Wand directory.";
                        return;
                    }

                    WeModPath = dialog.SelectedPath;
                    SavePath();
                    StatusMessage = null;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to pick path: {ex.Message}";
            }
        }

        private async Task OnEnableAsync()
        {
            try
            {
                if (!CanEnable())
                {
                    StatusMessage = "A valid Wand directory is required.";
                    return;
                }

                SavePath();
                var escapedPath = WeModPath.Replace("\"", "\\\"");
                var arguments = $"--enable-autopatch \"{escapedPath}\"";

                await Task.Run(() => ElevationHelper.RelaunchElevated(arguments));
                StatusMessage = "UAC prompt shown. Please confirm to enable auto-patch.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to enable: {ex.Message}";
            }
        }

        private async Task OnDisableAsync()
        {
            try
            {
                await Task.Run(() => ElevationHelper.RelaunchElevated("--disable-autopatch"));
                StatusMessage = "UAC prompt shown. Please confirm to disable auto-patch.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to disable: {ex.Message}";
            }
        }

        private void SavePath()
        {
            var config = _settingsStore.Load();
            config.Path = WeModPath;
            _settingsStore.Save(config);
        }
    }
}
