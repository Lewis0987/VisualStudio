using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GDM8261A_Tester
{
    /// <summary>
    /// MainForm 的 USB Relay 整合部分（UI）。
    /// 硬體邏輯封裝於 <see cref="UsbRelayController"/>，本檔僅負責 UI 呈現與呼叫。
    /// 透過 OnLoad / OnFormClosing 掛鉤，原始 MainForm.cs 不需改動（僅加上 partial）。
    /// </summary>
    public partial class MainForm
    {
        private readonly UsbRelayController _relay = new UsbRelayController();

        // 面板固定寬度，收合 / 展開固定高度，與主視窗邊距
        private const int RelayBoxWidth = 320;
        private const int RelayExpandedHeight = 160;
        private const int RelayCollapsedHeight = 36;
        private const int RelayMargin = 12;

        private bool _relayExpanded;

        private GroupBox _relayBox;
        private Panel _relayContent;
        private Button _relayToggle;
        private Button _relayRefreshBtn;
        private Button _relayConnectBtn;
        private Button _relayDisconnectBtn;
        private Label _relayConnStatus;

        private readonly Dictionary<int, Button> _relayOnButtons = new Dictionary<int, Button>();
        private readonly Dictionary<int, Button> _relayOffButtons = new Dictionary<int, Button>();
        private readonly Dictionary<int, Label> _relayLights = new Dictionary<int, Label>();
        private readonly Dictionary<int, Label> _relayTexts = new Dictionary<int, Label>();

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            BuildRelayPanel();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _relay.Dispose();
            base.OnFormClosing(e); // 觸發原始 MainForm_FormClosing，保留原本關閉流程
        }

        /// <summary>建立右下角可展開 / 收合的 USB Relay 控制 GroupBox，預設為收合狀態。</summary>
        private void BuildRelayPanel()
        {
            _relayBox = new GroupBox
            {
                Text = "USB Relay 控制",
                Size = new Size(RelayBoxWidth, RelayExpandedHeight),
                // 固定停靠右下角，主視窗縮放時跟著移動
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            Controls.Add(_relayBox);

            // 展開 / 收合按鈕：固定在面板右上角（位置於 PositionRelayPanel 內計算，確保不超出面板）
            _relayToggle = new Button
            {
                Text = "收合",
                Size = new Size(60, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                TabStop = false
            };
            _relayToggle.Click += (s, e) => ToggleRelayPanel();
            _relayBox.Controls.Add(_relayToggle);
            _relayToggle.BringToFront();

            // 內容區（收合時整塊隱藏）
            _relayContent = new Panel
            {
                Location = new Point(8, 18),
                Size = new Size(RelayBoxWidth - 16, RelayExpandedHeight - 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            _relayBox.Controls.Add(_relayContent);

            _relayRefreshBtn = new Button { Text = "重新偵測", Location = new Point(0, 4), Size = new Size(84, 26) };
            _relayRefreshBtn.Click += (s, e) => RefreshRelayDevices();
            _relayContent.Controls.Add(_relayRefreshBtn);

            _relayConnectBtn = new Button { Text = "連線", Location = new Point(90, 4), Size = new Size(60, 26) };
            _relayConnectBtn.Click += (s, e) => RelayConnect();
            _relayContent.Controls.Add(_relayConnectBtn);

            _relayDisconnectBtn = new Button { Text = "中斷連線", Location = new Point(156, 4), Size = new Size(84, 26) };
            _relayDisconnectBtn.Click += (s, e) => RelayDisconnect();
            _relayContent.Controls.Add(_relayDisconnectBtn);

            _relayConnStatus = new Label
            {
                Text = "USB Relay 未連線",
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(0, 38)
            };
            _relayContent.Controls.Add(_relayConnStatus);

            for (int ch = 1; ch <= 2; ch++)
            {
                int rowY = 60 + (ch - 1) * 32;
                int channel = ch;

                var label = new Label { Text = "Relay " + ch, AutoSize = true, Location = new Point(0, rowY + 4) };
                _relayContent.Controls.Add(label);

                var onButton = new Button
                {
                    Text = "ON",
                    Location = new Point(54, rowY),
                    Size = new Size(52, 26),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Green
                };
                onButton.Click += (s, e) => OnSetRelay(channel, true);
                _relayContent.Controls.Add(onButton);

                var offButton = new Button
                {
                    Text = "OFF",
                    Location = new Point(110, rowY),
                    Size = new Size(52, 26),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Firebrick
                };
                offButton.Click += (s, e) => OnSetRelay(channel, false);
                _relayContent.Controls.Add(offButton);

                var light = new Label
                {
                    Text = "●",
                    AutoSize = true,
                    Location = new Point(172, rowY + 4),
                    Font = new Font(Font.FontFamily, 11F, FontStyle.Bold),
                    ForeColor = Color.Gray
                };
                _relayContent.Controls.Add(light);

                var stateText = new Label
                {
                    Text = "未連線",
                    AutoSize = true,
                    Location = new Point(192, rowY + 4),
                    ForeColor = Color.Gray
                };
                _relayContent.Controls.Add(stateText);

                _relayOnButtons[ch] = onButton;
                _relayOffButtons[ch] = offButton;
                _relayLights[ch] = light;
                _relayTexts[ch] = stateText;
            }

            _relayBox.BringToFront();

            SetRelayControlsEnabled(false);
            RefreshRelayDevices();

            // 視窗縮放時重新計算面板位置
            Resize += (s, e) => PositionRelayPanel();

            // 預設收合
            _relayExpanded = false;
            _relayContent.Visible = false;
            _relayToggle.Text = "展開";
            PositionRelayPanel();
        }

        /// <summary>
        /// 依目前展開狀態，重新計算 Relay 面板的大小與位置（右下角，距主視窗 12px）。
        /// 不使用固定座標硬放，視窗縮放或 DPI 變動都會重新定位。
        /// </summary>
        private void PositionRelayPanel()
        {
            if (_relayBox == null)
                return;

            int h = _relayExpanded ? RelayExpandedHeight : RelayCollapsedHeight;

            _relayBox.Size = new Size(RelayBoxWidth, h);
            _relayBox.Location = new Point(
                ClientSize.Width - RelayMargin - RelayBoxWidth,
                ClientSize.Height - RelayMargin - h);

            // 展開 / 收合按鈕固定在面板右上角，且不超出面板
            _relayToggle.Top = 0;
            _relayToggle.Left = RelayBoxWidth - _relayToggle.Width - 12;
        }

        /// <summary>展開 / 收合：寬度固定 320，僅切換高度（收合 36 / 展開 160），底邊固定不跑版。</summary>
        private void ToggleRelayPanel()
        {
            _relayExpanded = !_relayExpanded;
            _relayContent.Visible = _relayExpanded;
            _relayToggle.Text = _relayExpanded ? "收合" : "展開";
            PositionRelayPanel();
        }

        private void RefreshRelayDevices()
        {
            if (_relay.IsConnected)
                return;

            _relayConnStatus.ForeColor = Color.Gray;
            _relayConnStatus.Text = _relay.DetectDevice()
                ? "USB Relay 已偵測，尚未連線"
                : "USB Relay 未連線 (未偵測到裝置)";
        }

        private void RelayConnect()
        {
            try
            {
                _relay.Connect();

                _relayConnStatus.ForeColor = Color.Green;
                _relayConnStatus.Text = "USB Relay 已連線";
                SetRelayControlsEnabled(true);

                // 連線後初始化為 OFF，使顯示與硬體一致
                for (int ch = 1; ch <= 2; ch++)
                {
                    try { _relay.SetRelay(ch, false); }
                    catch { }
                    UpdateRelayVisual(ch, false);
                }
            }
            catch (Exception ex)
            {
                _relay.Disconnect();
                _relayConnStatus.ForeColor = Color.Firebrick;
                _relayConnStatus.Text = "USB Relay 連線失敗";
                MessageBox.Show(this, ex.Message, "USB Relay 連線失敗",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RelayDisconnect()
        {
            _relay.Disconnect();
            _relayConnStatus.ForeColor = Color.Gray;
            _relayConnStatus.Text = "USB Relay 未連線";
            SetRelayControlsEnabled(false);
        }

        private void OnSetRelay(int channel, bool on)
        {
            if (!_relay.IsConnected)
            {
                MessageBox.Show(this, "請先連線到 USBRelay2。", "尚未連線",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _relay.SetRelay(channel, on);
                UpdateRelayVisual(channel, on);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "操作失敗",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                RelayDisconnect();
            }
        }

        /// <summary>
        /// 更新單一通道的狀態燈與文字 / 按鈕高亮。
        /// on == true → 綠燈 + 開啟；false → 紅燈 + 關閉；null → 灰燈 + 未連線。
        /// </summary>
        private void UpdateRelayVisual(int channel, bool? on)
        {
            Button onBtn, offBtn;
            if (_relayOnButtons.TryGetValue(channel, out onBtn) &&
                _relayOffButtons.TryGetValue(channel, out offBtn))
            {
                onBtn.BackColor = on == true ? Color.PaleGreen : SystemColors.Control;
                offBtn.BackColor = on == false ? Color.MistyRose : SystemColors.Control;
            }

            Label light, text;
            if (_relayLights.TryGetValue(channel, out light) &&
                _relayTexts.TryGetValue(channel, out text))
            {
                if (on == true)
                {
                    light.ForeColor = Color.Green;
                    text.Text = "開啟";
                    text.ForeColor = Color.Green;
                }
                else if (on == false)
                {
                    light.ForeColor = Color.Firebrick;
                    text.Text = "關閉";
                    text.ForeColor = Color.Firebrick;
                }
                else
                {
                    light.ForeColor = Color.Gray;
                    text.Text = "未連線";
                    text.ForeColor = Color.Gray;
                }
            }
        }

        private void SetRelayControlsEnabled(bool enabled)
        {
            foreach (Button b in _relayOnButtons.Values)
                b.Enabled = enabled;
            foreach (Button b in _relayOffButtons.Values)
                b.Enabled = enabled;

            _relayConnectBtn.Enabled = !enabled;
            _relayDisconnectBtn.Enabled = enabled;

            if (!enabled)
            {
                foreach (int ch in _relayOnButtons.Keys)
                    UpdateRelayVisual(ch, null);
            }
        }
    }
}
