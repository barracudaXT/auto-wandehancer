using System;
using System.Drawing;
using System.Windows.Forms;

namespace WandEnhancer.Core.Services
{
    public class NotificationService : INotificationService, IDisposable
    {
        private readonly NotifyIcon _icon;
        private bool _disposed;

        public NotificationService()
        {
            _icon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "WandEnhancer Auto-Patch",
                Visible = true
            };
        }

        public void ShowInfo(string title, string message) => Show(title, message, ToolTipIcon.Info);
        public void ShowWarning(string title, string message) => Show(title, message, ToolTipIcon.Warning);
        public void ShowError(string title, string message) => Show(title, message, ToolTipIcon.Error);

        private void Show(string title, string message, ToolTipIcon icon)
        {
            if (_disposed)
                return;

            try
            {
                _icon.BalloonTipTitle = title;
                _icon.BalloonTipText = message;
                _icon.BalloonTipIcon = icon;
                _icon.ShowBalloonTip(3000);
            }
            catch (Exception)
            {
                // Notifications are best-effort.
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _icon.Visible = false;
            _icon.Dispose();
        }
    }
}
