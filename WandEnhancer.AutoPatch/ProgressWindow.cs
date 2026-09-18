using System;
using System.Drawing;
using System.Windows.Forms;

namespace WandEnhancer.AutoPatch
{
    public class ProgressWindow : Form
    {
        private readonly Label _statusLabel;
        private readonly ProgressBar _progressBar;
        private readonly Button _retryButton;
        private readonly Button _openMainButton;

        public ProgressWindow()
        {
            Text = "Wand Enhancer Auto-Patch";
            AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(400, 160);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _statusLabel = new Label
            {
                Location = new Point(20, 20),
                Size = new Size(360, 20),
                Text = "Preparing..."
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(20, 50),
                Size = new Size(360, 20),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30
            };

            _retryButton = new Button
            {
                Text = "Retry",
                Location = new Point(220, 90),
                Size = new Size(75, 23),
                Visible = false
            };
            _retryButton.Click += (s, e) => RetryRequested?.Invoke(this, EventArgs.Empty);

            _openMainButton = new Button
            {
                Text = "Open WandEnhancer",
                Location = new Point(110, 90),
                Size = new Size(100, 23),
                Visible = false
            };
            _openMainButton.Click += (s, e) => OpenMainRequested?.Invoke(this, EventArgs.Empty);

            Controls.Add(_statusLabel);
            Controls.Add(_progressBar);
            Controls.Add(_retryButton);
            Controls.Add(_openMainButton);
        }

        public event EventHandler RetryRequested;
        public event EventHandler OpenMainRequested;

        public void SetStatus(string text) => RunOnUI(() => _statusLabel.Text = text);

        public void ShowSuccess(string message) => RunOnUI(() =>
        {
            _statusLabel.Text = message;
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 100;
            var timer = new Timer { Interval = 1500 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                SafeClose();
            };
            timer.Start();
        });

        public void ShowFailure(string message) => RunOnUI(() =>
        {
            _statusLabel.Text = message;
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 0;
            _retryButton.Visible = true;
            _openMainButton.Visible = true;
        });

        public void HideFailureButtons() => RunOnUI(() =>
        {
            _retryButton.Visible = false;
            _openMainButton.Visible = false;
        });

        public void SafeClose() => RunOnUI(() => base.Close());

        private void RunOnUI(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            if (InvokeRequired)
            {
                Invoke(action);
                return;
            }
            action();
        }
    }
}
