using System;
using System.Drawing;
using System.Windows.Forms;

namespace WandEnhancer.AutoPatch
{
    public class TrayAgent : ApplicationContext
    {
        private readonly NotifyIcon _icon;
        private readonly ToolStripMenuItem _enabledMenuItem;

        public event EventHandler PatchNowClicked;
        public event EventHandler OpenSettingsClicked;
        public event EventHandler ExitClicked;
        public event EventHandler WatcherEnabledChanged;

        public bool WatcherEnabled
        {
            get => _enabledMenuItem.Checked;
            set => _enabledMenuItem.Checked = value;
        }

        public TrayAgent()
        {
            _icon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "WandEnhancer Auto-Patch",
                Visible = true
            };

            var menu = new ContextMenuStrip();
            _enabledMenuItem = new ToolStripMenuItem("Watcher enabled", null, OnToggleEnabled) { Checked = true };
            menu.Items.Add(_enabledMenuItem);
            menu.Items.Add("Patch now", null, (s, e) => PatchNowClicked?.Invoke(this, e));
            menu.Items.Add("Open WandEnhancer", null, (s, e) => OpenSettingsClicked?.Invoke(this, e));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitClicked?.Invoke(this, e));

            _icon.ContextMenuStrip = menu;
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
