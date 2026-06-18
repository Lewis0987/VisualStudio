using System;
using System.Drawing;
using System.Windows.Forms;

namespace DX01_ShortCircuitTester
{
    /// <summary>
    /// 輕量 Toast 提示：右下角顯示一段訊息，停留數秒後淡出消失，不搶焦點、不阻擋操作。
    /// 用於登入 / 登出等不需使用者確認的提示（取代 MessageBox）。
    /// </summary>
    internal sealed class Toast : Form
    {
        private static Toast _current;

        private readonly Timer _timer = new Timer { Interval = 60 };
        private readonly int _holdMs;
        private readonly int _fadeMs;
        private readonly double _maxOpacity = 0.92;
        private int _elapsed;

        private Toast(string message, int holdMs, int fadeMs)
        {
            _holdMs = holdMs;
            _fadeMs = fadeMs;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            ControlBox = false;
            BackColor = Color.FromArgb(48, 48, 48);
            Opacity = _maxOpacity;

            var lbl = new Label
            {
                Text = message ?? "",
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold),
                Location = new Point(16, 12)
            };
            Controls.Add(lbl);
            ClientSize = new Size(lbl.PreferredWidth + 32, lbl.PreferredHeight + 24);

            _timer.Tick += OnTick;
        }

        // 不在顯示時搶走焦點
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000;
                const int WS_EX_TOPMOST = 0x00000008;
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            _elapsed += _timer.Interval;
            if (_elapsed < _holdMs)
                return;

            double p = 1.0 - (double)(_elapsed - _holdMs) / _fadeMs;
            if (p <= 0)
            {
                Close();
                return;
            }
            Opacity = _maxOpacity * p;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            if (_current == this)
                _current = null;
            base.OnFormClosed(e);
        }

        /// <summary>於擁有者視窗右下角顯示 Toast（預設停留 2.2 秒、淡出 0.8 秒）。</summary>
        public static void Show(Form owner, string message, int holdMs = 2200, int fadeMs = 800)
        {
            try
            {
                if (_current != null && !_current.IsDisposed)
                    _current.Close();
            }
            catch { }

            var t = new Toast(message, holdMs, fadeMs);
            _current = t;

            Rectangle b = (owner != null && !owner.IsDisposed)
                ? owner.Bounds
                : Screen.PrimaryScreen.WorkingArea;
            t.Location = new Point(b.Right - t.Width - 24, b.Bottom - t.Height - 60);

            if (owner != null && !owner.IsDisposed)
                t.Show(owner);
            else
                t.Show();
        }
    }
}
