using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DX01_ShortCircuitTester
{
    /// <summary>
    /// V2.1：主畫面右下角可收合的「USB Relay 控制」小視窗（參考 D:\GDM-8261A-Tester-2）。
    /// 手動切換 Relay1 / Relay2；重用既有的 _relay（RealRelayController），
    /// 切換後透過 RelayChanged 事件同步主畫面 Relay 狀態與 Debug Log。
    /// 規則：未連線 / 測試進行中 → ON/OFF 停用，避免人工干擾測試。
    /// </summary>
    public partial class MainForm
    {
        private const int RelayBoxWidth = 300;
        private const int RelayExpandedHeight = 122;
        private const int RelayCollapsedHeight = 30;   // 標題列高度（收合時整體高度）
        private const int RelayRightMargin = 8;        // 距表格右緣
        private const int RelayBottomMargin = 6;       // 距表格下緣（貼齊下方框線）

        private bool _relayPanelExpanded;
        private GroupBox _relayPanelBox;
        private Panel _relayPanelContent;
        private Button _relayPanelToggle;
        private Label _relayPanelTitle;
        private Label _relayPanelStatus;

        private readonly Dictionary<int, Button> _relayPanelOn = new Dictionary<int, Button>();
        private readonly Dictionary<int, Button> _relayPanelOff = new Dictionary<int, Button>();
        private readonly Dictionary<int, Label> _relayPanelLight = new Dictionary<int, Label>();
        private readonly Dictionary<int, Label> _relayPanelText = new Dictionary<int, Label>();

        /// <summary>建立右下角可展開 / 收合的 Relay 控制小視窗，預設展開。</summary>
        private void BuildRelayPanel()
        {
            if (_relayPanelBox != null)
                return;

            // GroupBox 僅當作有邊框的容器（不用內建 caption，改用可垂直置中的標題 Label）
            _relayPanelBox = new GroupBox
            {
                Text = "",
                Size = new Size(RelayBoxWidth, RelayExpandedHeight),
                Font = new Font("Microsoft JhengHei UI", 9F),   // 整體 9pt（子控制項繼承）
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            Controls.Add(_relayPanelBox);

            // 標題：垂直置中於標題列（Top 由 PositionRelayPanel 計算）
            _relayPanelTitle = new Label
            {
                Text = "USB Relay 控制",
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold),
                Location = new Point(10, 6)
            };
            _relayPanelBox.Controls.Add(_relayPanelTitle);
            _relayPanelTitle.BringToFront();

            _relayPanelToggle = new Button
            {
                Text = "收合",
                Size = new Size(52, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                TabStop = false
            };
            _relayPanelToggle.Click += (s, e) => ToggleRelayPanel();
            _relayPanelBox.Controls.Add(_relayPanelToggle);
            _relayPanelToggle.BringToFront();

            // 內容區從標題列下方開始（收合時隱藏）
            _relayPanelContent = new Panel
            {
                Location = new Point(8, RelayCollapsedHeight),
                Size = new Size(RelayBoxWidth - 16, RelayExpandedHeight - RelayCollapsedHeight - RelayBottomMargin),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            _relayPanelBox.Controls.Add(_relayPanelContent);

            _relayPanelStatus = new Label
            {
                Text = "USB Relay 未連線",
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(0, 2)
            };
            _relayPanelContent.Controls.Add(_relayPanelStatus);

            for (int ch = 1; ch <= 2; ch++)
            {
                int rowY = 24 + (ch - 1) * 30;
                int channel = ch;

                var label = new Label { Text = "Relay " + ch, AutoSize = true, Location = new Point(0, rowY + 5) };
                _relayPanelContent.Controls.Add(label);

                var onBtn = new Button
                {
                    Text = "ON",
                    Location = new Point(58, rowY),
                    Size = new Size(54, 26),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Green
                };
                onBtn.Click += (s, e) => OnRelayPanelSet(channel, true);
                _relayPanelContent.Controls.Add(onBtn);

                var offBtn = new Button
                {
                    Text = "OFF",
                    Location = new Point(116, rowY),
                    Size = new Size(54, 26),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Firebrick
                };
                offBtn.Click += (s, e) => OnRelayPanelSet(channel, false);
                _relayPanelContent.Controls.Add(offBtn);

                var lightDot = new Label
                {
                    Text = "●",
                    AutoSize = true,
                    Location = new Point(182, rowY + 4),
                    Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold),
                    ForeColor = Color.Gray
                };
                _relayPanelContent.Controls.Add(lightDot);

                var stateText = new Label
                {
                    Text = "未連線",
                    AutoSize = true,
                    Location = new Point(202, rowY + 5),
                    ForeColor = Color.Gray
                };
                _relayPanelContent.Controls.Add(stateText);

                _relayPanelOn[ch] = onBtn;
                _relayPanelOff[ch] = offBtn;
                _relayPanelLight[ch] = lightDot;
                _relayPanelText[ch] = stateText;
            }

            _relayPanelBox.BringToFront();

            // 預設收合（只顯示標題列）
            _relayPanelExpanded = false;
            _relayPanelContent.Visible = false;
            _relayPanelToggle.Text = "展開";

            Resize += (s, e) => PositionRelayPanel();
            // 表格大小變動（視窗縮放 / 版面調整）時，重算位置以保持在灰色表格區塊內
            if (dgvResults != null)
                dgvResults.SizeChanged += (s, e) => PositionRelayPanel();

            // 切換任何 Tab（Test / Settings / Debug）都自動收合；只在 Test 頁顯示
            tabMain.SelectedIndexChanged += (s, e) =>
            {
                CollapseRelayPanel();
                UpdateRelayPanelVisibility();
            };

            PositionRelayPanel();
            UpdateRelayPanelVisibility();
        }

        /// <summary>收合 Relay 小視窗：高度回收合高度、隱藏內容、按鈕文字改「展開」。</summary>
        private void CollapseRelayPanel()
        {
            if (_relayPanelBox == null || !_relayPanelExpanded)
                return;
            _relayPanelExpanded = false;
            _relayPanelContent.Visible = false;
            _relayPanelToggle.Text = "展開";
            PositionRelayPanel();
        }

        /// <summary>Relay 小視窗僅在 Test 頁顯示。</summary>
        private void UpdateRelayPanelVisibility()
        {
            if (_relayPanelBox == null)
                return;
            _relayPanelBox.Visible = (tabMain.SelectedTab == tabTest);
            if (_relayPanelBox.Visible)
                _relayPanelBox.BringToFront();
        }

        /// <summary>
        /// 依展開狀態重算 Relay 小視窗位置，**固定在 DataGridView 灰色區塊的右下角內**：
        /// x = grid.Right - width - 10、y = grid.Bottom - height - 10（高度不足時向上展開，不往下超出）。
        /// 以螢幕座標換算，確保視窗縮放時仍留在表格內、不壓到狀態列。
        /// </summary>
        private void PositionRelayPanel()
        {
            if (_relayPanelBox == null || dgvResults == null || !dgvResults.IsHandleCreated)
                return;

            int h = _relayPanelExpanded ? RelayExpandedHeight : RelayCollapsedHeight;
            _relayPanelBox.Size = new Size(RelayBoxWidth, h);

            // 將 dgvResults（灰色表格）邊界換算到主視窗 client 座標
            Rectangle grid = RectangleToClient(dgvResults.RectangleToScreen(dgvResults.ClientRectangle));

            // 貼齊表格右下角：右 8px、下 6px
            int x = grid.Right - RelayBoxWidth - RelayRightMargin;
            int y = grid.Bottom - h - RelayBottomMargin;

            // 不可超出表格左 / 上緣（空間不足時靠左、向上展開，絕不往下 / 往外超出）
            if (x < grid.Left + RelayRightMargin) x = grid.Left + RelayRightMargin;
            if (y < grid.Top + RelayBottomMargin) y = grid.Top + RelayBottomMargin;

            _relayPanelBox.Location = new Point(x, y);

            // 標題文字與展開/收合按鈕：垂直置中於標題列（同一水平中心線）
            _relayPanelTitle.Top = Math.Max(2, (RelayCollapsedHeight - _relayPanelTitle.Height) / 2);
            _relayPanelTitle.Left = 10;
            _relayPanelToggle.Top = Math.Max(2, (RelayCollapsedHeight - _relayPanelToggle.Height) / 2);
            _relayPanelToggle.Left = RelayBoxWidth - _relayPanelToggle.Width - 10;
        }

        private void ToggleRelayPanel()
        {
            _relayPanelExpanded = !_relayPanelExpanded;
            _relayPanelContent.Visible = _relayPanelExpanded;
            _relayPanelToggle.Text = _relayPanelExpanded ? "收合" : "展開";
            PositionRelayPanel();
        }

        /// <summary>手動切換單一通道：以目前代碼結合新狀態組成 2 位代碼，再呼叫 _relay.SetRelay。</summary>
        private void OnRelayPanelSet(int channel, bool on)
        {
            if (_running || !_relay.IsConnected)
                return;

            string cur = string.IsNullOrEmpty(_relay.CurrentCode) ? "00" : _relay.CurrentCode;
            char c1 = cur.Length > 0 ? cur[0] : '0';
            char c2 = cur.Length > 1 ? cur[1] : '0';
            if (channel == 1) c1 = on ? '1' : '0';
            else c2 = on ? '1' : '0';
            string code = new string(new[] { c1, c2 });

            try
            {
                _relay.SetRelay(code); // 觸發 RelayChanged → 同步主畫面 + Debug Log + 小視窗燈號
            }
            catch (Exception ex)
            {
                MsgBox.Show(this, "操作失敗", "Relay 切換失敗:\n" + ex.Message, MessageBoxIcon.Error, "確定");
                try { _relay.Disconnect(); } catch { }
                UpdateConnStatus();
            }
        }

        /// <summary>依連線 / 測試狀態，啟用或停用 Relay 小視窗的手動控制（執行緒安全 + null 安全）。</summary>
        private void UpdateRelayPanelState()
        {
            if (_relayPanelBox == null)
                return;
            if (_relayPanelBox.InvokeRequired)
            {
                try { _relayPanelBox.BeginInvoke(new Action(UpdateRelayPanelState)); } catch { }
                return;
            }

            bool connected = _relay != null && _relay.IsConnected;
            bool enabled = connected && !_running;

            foreach (var b in _relayPanelOn.Values) b.Enabled = enabled;
            foreach (var b in _relayPanelOff.Values) b.Enabled = enabled;

            if (connected)
            {
                _relayPanelStatus.Text = _running ? "測試進行中（手動控制已鎖定）" : "USB Relay 已連線";
                _relayPanelStatus.ForeColor = _running ? Color.DarkOrange : OkGreen;
                UpdateRelayPanelVisual(string.IsNullOrEmpty(_relay.CurrentCode) ? "00" : _relay.CurrentCode);
            }
            else
            {
                _relayPanelStatus.Text = "USB Relay 未連線";
                _relayPanelStatus.ForeColor = Color.Gray;
                for (int ch = 1; ch <= 2; ch++)
                    SetRelayPanelLight(ch, null);
            }
        }

        /// <summary>依 2 位 Relay 代碼更新兩通道燈號 / 文字（執行緒安全 + null 安全）。</summary>
        private void UpdateRelayPanelVisual(string code)
        {
            if (_relayPanelBox == null)
                return;
            if (_relayPanelBox.InvokeRequired)
            {
                try { _relayPanelBox.BeginInvoke(new Action(() => UpdateRelayPanelVisual(code))); } catch { }
                return;
            }
            if (!(_relay != null && _relay.IsConnected))
                return;

            string c = string.IsNullOrEmpty(code) ? "00" : code;
            SetRelayPanelLight(1, c.Length > 0 && c[0] == '1');
            SetRelayPanelLight(2, c.Length > 1 && c[1] == '1');
        }

        private void SetRelayPanelLight(int channel, bool? on)
        {
            Button onBtn, offBtn;
            if (_relayPanelOn.TryGetValue(channel, out onBtn) && _relayPanelOff.TryGetValue(channel, out offBtn))
            {
                onBtn.BackColor = on == true ? Color.PaleGreen : SystemColors.Control;
                offBtn.BackColor = on == false ? Color.MistyRose : SystemColors.Control;
            }

            Label light, text;
            if (_relayPanelLight.TryGetValue(channel, out light) && _relayPanelText.TryGetValue(channel, out text))
            {
                if (on == true) { light.ForeColor = Color.Green; text.Text = "開啟"; text.ForeColor = Color.Green; }
                else if (on == false) { light.ForeColor = Color.Firebrick; text.Text = "關閉"; text.ForeColor = Color.Firebrick; }
                else { light.ForeColor = Color.Gray; text.Text = "未連線"; text.ForeColor = Color.Gray; }
            }
        }
    }
}
