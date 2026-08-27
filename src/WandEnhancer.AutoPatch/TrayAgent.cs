using System;
using System.Drawing;
using System.Windows.Forms;
using WandEnhancer.Core.Services;

namespace WandEnhancer.AutoPatch
{
    public class TrayAgent : ApplicationContext, INotificationService
    {
        private readonly NotifyIcon _icon;
        private readonly ToolStripMenuItem _enabledMenuItem;
        private readonly ToolStripMenuItem _updateMenuItem;

        public event EventHandler PatchNowClicked;
        public event EventHandler OpenSettingsClicked;
        public event EventHandler ExitClicked;
        public event EventHandler WatcherEnabledChanged;
        public event EventHandler CheckForUpdatesClicked;
        public event EventHandler InstallUpdateClicked;

        public bool WatcherEnabled
        {
            get => _enabledMenuItem.Checked;
            set => _enabledMenuItem.Checked = value;
        }

        private bool _updatePending;

        public TrayAgent()
        {
            _icon = new NotifyIcon
            {
                Icon = LoadEmbeddedIcon(),
                Text = "WandEnhancer Auto-Patch",
                Visible = true
            };

            var menu = new ContextMenuStrip();
            _enabledMenuItem = new ToolStripMenuItem("Watcher enabled", null, OnToggleEnabled) { Checked = true };
            menu.Items.Add(_enabledMenuItem);
            menu.Items.Add("Patch now", null, (s, e) => PatchNowClicked?.Invoke(this, e));
            _updateMenuItem = new ToolStripMenuItem("Check for updates", null, OnUpdateMenuClicked);
            menu.Items.Add(_updateMenuItem);
            menu.Items.Add("Open WandEnhancer", null, (s, e) => OpenSettingsClicked?.Invoke(this, e));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitClicked?.Invoke(this, e));

            _icon.ContextMenuStrip = menu;
        }

        private void OnUpdateMenuClicked(object sender, EventArgs e)
        {
            if (_updatePending)
                InstallUpdateClicked?.Invoke(this, e);
            else
                CheckForUpdatesClicked?.Invoke(this, e);
        }

        public void ShowCheckingForUpdates()
        {
            if (_icon.ContextMenuStrip.InvokeRequired)
            {
                _icon.ContextMenuStrip.BeginInvoke(new Action(ShowCheckingForUpdates));
                return;
            }
            _updateMenuItem.Text = "Checking for updates...";
            _updateMenuItem.Enabled = false;
        }

        public void ShowUpdateAvailable(string version)
        {
            if (_icon.ContextMenuStrip.InvokeRequired)
            {
                _icon.ContextMenuStrip.BeginInvoke(new Action(() => ShowUpdateAvailable(version)));
                return;
            }
            _updatePending = true;
            _updateMenuItem.Enabled = true;
            _updateMenuItem.Text = $"Update available: {version}";
            _icon.ShowBalloonTip(5000, "WandEnhancer", $"Version {version} is available. Right-click the tray icon to update.", ToolTipIcon.Info);
        }

        public void ShowUpToDate()
        {
            if (_icon.ContextMenuStrip.InvokeRequired)
            {
                _icon.ContextMenuStrip.BeginInvoke(new Action(ShowUpToDate));
                return;
            }
            _updatePending = false;
            _updateMenuItem.Enabled = true;
            _updateMenuItem.Text = "Check for updates";
        }

        public void ShowUpdateCheckFailed()
        {
            if (_icon.ContextMenuStrip.InvokeRequired)
            {
                _icon.ContextMenuStrip.BeginInvoke(new Action(ShowUpdateCheckFailed));
                return;
            }
            _updateMenuItem.Enabled = true;
            _updateMenuItem.Text = "Check for updates";
        }

        public void ShowDownloading(int progressPercent)
        {
            if (_icon.ContextMenuStrip.InvokeRequired)
            {
                _icon.ContextMenuStrip.BeginInvoke(new Action(() => ShowDownloading(progressPercent)));
                return;
            }
            _updateMenuItem.Enabled = false;
            _updateMenuItem.Text = $"Downloading update... {progressPercent}%";
        }

        public void ShowInstalling()
        {
            if (_icon.ContextMenuStrip.InvokeRequired)
            {
                _icon.ContextMenuStrip.BeginInvoke(new Action(ShowInstalling));
                return;
            }
            _updateMenuItem.Enabled = false;
            _updateMenuItem.Text = "Installing update...";
        }

        public void ShowInfo(string title, string message) => ShowBalloon(title, message, ToolTipIcon.Info);
        public void ShowWarning(string title, string message) => ShowBalloon(title, message, ToolTipIcon.Warning);
        public void ShowError(string title, string message) => ShowBalloon(title, message, ToolTipIcon.Error);

        private void ShowBalloon(string title, string message, ToolTipIcon tipIcon)
        {
            if (_icon.ContextMenuStrip.InvokeRequired)
            {
                _icon.ContextMenuStrip.BeginInvoke(new Action(() => ShowBalloon(title, message, tipIcon)));
                return;
            }
            _icon.ShowBalloonTip(3000, title, message, tipIcon);
        }

        private static Icon LoadEmbeddedIcon()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                const string resourceName = "WandEnhancer.AutoPatch.appicon.ico";
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                        return new Icon(stream);
                }
            }
            catch
            {
                // Fall back to the default icon if the embedded resource can't be loaded.
            }
            return SystemIcons.Application;
        }

        private void OnToggleEnabled(object sender, EventArgs e)
        {
            _enabledMenuItem.Checked = !_enabledMenuItem.Checked;
            WatcherEnabledChanged?.Invoke(this, e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _icon.Visible = false;
                _icon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
