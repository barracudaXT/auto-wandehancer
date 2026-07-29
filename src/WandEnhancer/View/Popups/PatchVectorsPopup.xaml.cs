using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WandEnhancer.Core.Models;

namespace WandEnhancer.View.Popups
{
    public partial class PatchVectorsPopup : UserControl
    {
        private const string JavaScriptDialogFilter = "JavaScript files (*.js)|*.js";
        private const string JavaScriptFileExtension = ".js";

        private readonly Action<PatchConfig> _onApply;
        private readonly PatchConfig _savedConfig;
        private readonly ObservableCollection<SelectedScript> _selectedScripts = new ObservableCollection<SelectedScript>();

        public PatchVectorsPopup(Action<PatchConfig> onApply, PatchConfig savedConfig = null)
        {
            _onApply = onApply;
            _savedConfig = savedConfig;
            InitializeComponent();
            ScriptList.ItemsSource = _selectedScripts;
            RestoreFromConfig(savedConfig);
            UpdateScriptsEmptyState();
        }

        private void RestoreFromConfig(PatchConfig config)
        {
            if (config == null)
            {
                return;
            }

            if (config.PatchTypes != null)
            {
                ActivateProBox.IsChecked = config.PatchTypes.Contains(EPatchType.ActivatePro);
                DisableUpdateBox.IsChecked = config.PatchTypes.Contains(EPatchType.DisableUpdates);
                DevToolsHotkeyBox.IsChecked = config.PatchTypes.Contains(EPatchType.DevToolsOnF12);
                RemoteWebPanelPreviewBox.IsChecked = config.PatchTypes.Contains(EPatchType.RemoteWebPanelPreview);
            }

            if (config.CustomScriptPaths != null)
            {
                foreach (var path in config.CustomScriptPaths)
                {
                    AddScript(path);
                }
            }
        }

        private void OnAddScriptClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = JavaScriptDialogFilter,
                Multiselect = true,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            foreach (var path in dialog.FileNames.Where(IsJavaScriptFile))
            {
                AddScript(path);
            }

            if (_selectedScripts.Count > 0)
            {
                RemoteWebPanelPreviewBox.IsChecked = true;
            }

            UpdateScriptsEmptyState();
        }

        private void OnRemoveScriptClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var script = button?.Tag as SelectedScript;
            if (script == null)
            {
                return;
            }

            _selectedScripts.Remove(script);
            UpdateScriptsEmptyState();
        }

        private void OnPatchButtonClick(object sender, RoutedEventArgs e)
        {
            if (ActivateProBox.IsChecked != true && DisableUpdateBox.IsChecked != true &&
                DevToolsHotkeyBox.IsChecked != true && RemoteWebPanelPreviewBox.IsChecked != true)
            {
                return;
            }
            
            var result = new HashSet<EPatchType>();
            if (ActivateProBox.IsChecked == true)
            {
                result.Add(EPatchType.ActivatePro);
            }

            if (DisableUpdateBox.IsChecked == true)
            {
                result.Add(EPatchType.DisableUpdates);
            }

            if (DevToolsHotkeyBox.IsChecked == true)
            {
                result.Add(EPatchType.DevToolsOnF12);
            }

            if (RemoteWebPanelPreviewBox.IsChecked == true)
            {
                result.Add(EPatchType.RemoteWebPanelPreview);
            }

            var newConfig = _savedConfig != null ? new PatchConfig
            {
                Path = _savedConfig.Path,
                AutoApplyPatches = _savedConfig.AutoApplyPatches
            } : new PatchConfig();

            newConfig.PatchTypes = result;
            newConfig.CustomScriptPaths = _selectedScripts.Select(script => script.FullPath).ToList();

            _onApply(newConfig);
        }

        private void AddScript(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (_selectedScripts.Any(script => string.Equals(script.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _selectedScripts.Add(new SelectedScript(fullPath));
        }

        private static bool IsJavaScriptFile(string path)
        {
            return File.Exists(path) && string.Equals(Path.GetExtension(path), JavaScriptFileExtension, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateScriptsEmptyState()
        {
            NoScriptsText.Visibility = _selectedScripts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private sealed class SelectedScript
        {
            public SelectedScript(string fullPath)
            {
                FullPath = fullPath;
                FileName = Path.GetFileName(fullPath);
            }

            public string FullPath { get; }

            public string FileName { get; }
        }
    }
}