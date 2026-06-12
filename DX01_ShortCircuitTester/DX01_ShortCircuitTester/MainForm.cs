using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using DX01_ShortCircuitTester.Device;
using DX01_ShortCircuitTester.Models;
using DX01_ShortCircuitTester.Services;

namespace DX01_ShortCircuitTester
{
    /// <summary>
    /// DX01 外殼短路流程測試主畫面（真實設備版）。
    /// UI 與流程不變；設備改用 RealGdm8261AController（RS-232）與 RealRelayController（USB HID）。
    /// </summary>
    public partial class MainForm : Form
    {
        private RealRelayController _relay;
        private RealGdm8261AController _meter;
        private DX01TestFlow _flow;

        private CancellationTokenSource _cts;
        private bool _running;

        // 已測試過的條碼 → 結果（OK / NG），供「重覆測試確認」使用（整個 session 保留）
        private readonly Dictionary<string, string> _testedBarcodes = new Dictionary<string, string>();

        // 避免同一次斷線重複跳出「LAN 連線中斷」提示；連線成功後重置
        private bool _lanLostShown;

        // USB Relay 是否被偵測到（由背景監控更新；不代表已連線）
        private bool _relayPresent;
        private bool _relayConnectedLast;
        // 測試中偵測到 USB Relay 被拔除（用於結束後顯示 Relay 異常）
        private bool _relayLostDuringTest;

        private static readonly Color OkGreen = Color.FromArgb(46, 160, 67);
        private static readonly Color NgRed = Color.FromArgb(211, 47, 47);

        // Test 頁底部連線狀態用色：已連線=綠、未連線=紅、連線中=橘
        private enum ConnState { Disconnected, Connecting, Connected }
        private static readonly Color ConnConnected = Color.Green;
        private static readonly Color ConnDisconnected = Color.Red;
        private static readonly Color ConnConnecting = Color.Orange;

        /// <summary>更新單一連線狀態標籤的文字與顏色（電表 / Relay 各自獨立判斷）。</summary>
        private static void SetConnLabel(Label label, string name, ConnState state)
        {
            string text;
            Color color;
            switch (state)
            {
                case ConnState.Connected: text = "已連線"; color = ConnConnected; break;
                case ConnState.Connecting: text = "連線中"; color = ConnConnecting; break;
                default: text = "未連線"; color = ConnDisconnected; break;
            }
            label.Text = name + ": " + text;
            label.ForeColor = color;
        }

        private System.Windows.Forms.Timer _okMsgTimer;

        // LAN 背景連線監控（Heartbeat）：每數秒靜默 *IDN? 確認連線存活
        private System.Windows.Forms.Timer _heartbeatTimer;

        // USB Relay 背景監控：每 1 秒偵測 VID/PID 插拔，自動連線 / 斷線
        private System.Windows.Forms.Timer _relayMonitorTimer;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            AppSettings.Load();

            // 鮑率選項
            cbGdmBaud.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });

            InitDevices();
            BuildDebugUi();
            RefreshGdmPorts();

            SetRunningState(false);
            SetResult("待測", Color.DimGray, Color.Gainsboro);

            txtBarcode.KeyDown += TxtBarcode_KeyDown;
            txtBarcode.Enter += (s, ev) => txtBarcode.SelectAll(); // 取得焦點時全選，方便覆蓋

            _okMsgTimer = new System.Windows.Forms.Timer { Interval = Math.Max(1, AppSettings.Current.PopupSeconds) * 1000 };
            _okMsgTimer.Tick += (s, ev) => { _okMsgTimer.Stop(); lblBarcodeMsg.Text = ""; };

            // GDM 連線方式（Serial / LAN）：依設定載入並切換顯示
            rbSerial.CheckedChanged += (s, ev) => UpdateGdmInterfaceUi();
            rbLan.CheckedChanged += (s, ev) => UpdateGdmInterfaceUi();
            LoadConnectionUiFromSettings();
            UpdateGdmInterfaceUi();
            ApplyUiSettings();

            // 啟動時初始化設備並嘗試連線（失敗則顯示未連線，不跳錯）
            TryAutoConnect();
            try { _relayPresent = _relay.DetectDevice(); } catch { _relayPresent = false; }
            UpdateConnStatus();
            UpdateSummaryStats(null);

            // LAN 背景斷線偵測：間隔由 Config 控制（GdmMonitorIntervalMs，預設 1 秒）
            _heartbeatTimer = new System.Windows.Forms.Timer
            {
                Interval = Math.Max(250, AppSettings.Current.GdmMonitorIntervalMs)
            };
            _heartbeatTimer.Tick += Heartbeat_Tick;
            _heartbeatTimer.Start();

            // USB Relay 背景偵測：每 1 秒偵測插拔（只更新狀態，不自動連線）
            _relayMonitorTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _relayMonitorTimer.Tick += RelayMonitor_Tick;
            _relayMonitorTimer.Start();

            tabMain.SelectedTab = tabTest;
            txtBarcode.Focus();
        }

        /// <summary>
        /// LAN 背景斷線偵測（待機時）：靜默送出 *IDN?。失敗即判定斷線。
        /// 待機斷線「不跳 Popup」，只更新 UI（電表未連線/紅）並寫 Debug Log。
        /// 不自動重連——使用者需手動按「連線」。測試中由流程自行偵測（不在此處理）。
        /// </summary>
        private void Heartbeat_Tick(object sender, EventArgs e)
        {
            if (_running)               // 測試進行中由流程自行通訊，不重複偵測
                return;
            if (!_meter.UseLan)         // 只監控 LAN
                return;
            if (!_meter.IsConnected)    // 未連線不需偵測
                return;

            if (!_meter.PingDevice())
            {
                // PingDevice 失敗已 DropConnection → IsConnected=false
                if (_debugLog != null)
                    _debugLog.Write(LogKind.Error, "GDM LAN disconnected.");
                UpdateConnStatus();     // 立即更新「電表：未連線」（紅）
            }
        }

        /// <summary>
        /// USB Relay 背景監控：每秒偵測 VID/PID 是否在線。
        /// 插入且未連線 → 自動連線；拔除且仍連線 → 自動斷線。狀態變動即刻更新 UI。
        /// 測試進行中不介入（由流程的 Relay 異常處理負責），避免併發存取 HID。
        /// </summary>
        private void RelayMonitor_Tick(object sender, EventArgs e)
        {
            bool present;
            try { present = _relay.DetectDevice(); }
            catch { present = false; }

            if (_running)
            {
                // 測試中 USB 被拔除 → 立即停止流程（標記 Relay 異常，由結束流程顯示 Popup）
                if (!present && _relay.IsConnected)
                {
                    _relayLostDuringTest = true;
                    try { _relay.Disconnect(); } catch { }
                    try { if (_cts != null) _cts.Cancel(); } catch { }
                }
                return; // 測試中不改連線 / 不更新按鈕，結束後再刷新
            }

            // 待機：USB 被拔除且仍標記連線 → 釋放（不自動重連、不自動 Connect）
            if (!present && _relay.IsConnected)
            {
                try { _relay.Disconnect(); }
                catch { }
            }

            // 僅在偵測 / 連線狀態變化時刷新 UI（避免每秒重繪）
            bool connected = _relay.IsConnected;
            if (present != _relayPresent || connected != _relayConnectedLast)
            {
                _relayPresent = present;
                _relayConnectedLast = connected;
                UpdateConnStatus();
            }
        }

        /// <summary>建立真實設備控制器與測試流程。</summary>
        private void InitDevices()
        {
            _relay = new RealRelayController();
            _meter = new RealGdm8261AController();

            _flow = new DX01TestFlow(_relay, _meter);
            _flow.StepStarted += Flow_StepStarted;
            _flow.RelayChanged += Flow_RelayChanged;
            _flow.Measured += Flow_Measured;
            _flow.StepCompleted += Flow_StepCompleted;
            _flow.StatusChanged += Flow_StatusChanged;
        }

        /// <summary>啟動時嘗試連線（Relay 由 HID 自動偵測，電表用目前選擇的 COM）。</summary>
        private void TryAutoConnect()
        {
            // USB Relay 不自動連線（需使用者手動按「連線」）；背景只偵測 USB 是否存在。

            // 僅在 Serial 且已選 COM 時自動連線；LAN 不自動連（避免 TCP 逾時阻塞啟動）
            if (!rbLan.Checked && cbGdmPort.SelectedItem != null)
            {
                try
                {
                    ApplyGdmConnectionSettings();
                    _meter.Connect();
                }
                catch { /* COM 不對或未接，維持未連線 */ }
            }
        }

        /// <summary>依目前選擇的連線方式，顯示 Serial 或 LAN 欄位。</summary>
        private void UpdateGdmInterfaceUi()
        {
            bool lan = rbLan.Checked;
            lblGdmPortCap.Visible = !lan;
            cbGdmPort.Visible = !lan;
            btnGdmRefresh.Visible = !lan;
            lblGdmBaudCap.Visible = !lan;
            cbGdmBaud.Visible = !lan;

            lblGdmIpCap.Visible = lan;
            txtGdmIp.Visible = lan;
            lblGdmTcpPortCap.Visible = lan;
            txtGdmPort.Visible = lan;
        }

        /// <summary>把 UI 的連線參數套到電表控制器與 AppSettings（Serial / LAN）。</summary>
        private void ApplyGdmConnectionSettings()
        {
            var c = AppSettings.Current;
            if (rbLan.Checked)
            {
                c.ConnectionMode = GdmConnectionMode.Lan;
                _meter.UseLan = true;
                _meter.Ip = txtGdmIp.Text.Trim();
                int port;
                _meter.TcpPort = int.TryParse(txtGdmPort.Text.Trim(), out port) ? port : 23;
                c.Ip = _meter.Ip;
                c.TcpPort = _meter.TcpPort;
            }
            else
            {
                c.ConnectionMode = GdmConnectionMode.Serial;
                _meter.UseLan = false;
                _meter.PortName = cbGdmPort.SelectedItem != null ? cbGdmPort.SelectedItem.ToString() : null;
                _meter.BaudRate = GetSelectedBaud();
                c.ComBaud = _meter.BaudRate;
            }
        }

        /// <summary>依 AppSettings 載入連線方式 UI（IP/Port/Baud/Radio）。</summary>
        private void LoadConnectionUiFromSettings()
        {
            var c = AppSettings.Current;
            txtGdmIp.Text = c.Ip;
            txtGdmPort.Text = c.TcpPort.ToString();
            string baud = c.ComBaud.ToString();
            cbGdmBaud.SelectedItem = cbGdmBaud.Items.Contains(baud) ? baud : "115200";
            if (c.ConnectionMode == GdmConnectionMode.Lan)
                rbLan.Checked = true;
            else
                rbSerial.Checked = true;
        }

        /// <summary>套用 UI / 執行參數（字級、Popup 秒數、Relay VID/PID 顯示）。</summary>
        private void ApplyUiSettings()
        {
            var c = AppSettings.Current;
            lblCurrentStep.Font = new Font(lblCurrentStep.Font.FontFamily, Math.Max(8, c.StepFontSize), FontStyle.Bold);
            if (_okMsgTimer != null)
                _okMsgTimer.Interval = Math.Max(1, c.PopupSeconds) * 1000;
            if (lblRelayInfo != null)
                lblRelayInfo.Text = "USB HID 自動偵測  VID/PID: " + c.VendorIdHex + " / " + c.ProductIdHex;
        }

        /// <summary>開啟參數設定對話框，關閉後刷新相關 UI。</summary>
        private void OpenSettingForm()
        {
            using (var f = new SettingForm())
            {
                f.ShowDialog(this);
            }
            LoadConnectionUiFromSettings();
            UpdateGdmInterfaceUi();
            ApplyUiSettings();
            UpdateConnStatus();
        }

        #region 設備設定頁

        private void RefreshGdmPorts()
        {
            string current = cbGdmPort.SelectedItem as string;
            cbGdmPort.Items.Clear();
            foreach (string p in SerialPort.GetPortNames())
                cbGdmPort.Items.Add(p);

            if (cbGdmPort.Items.Count > 0)
            {
                cbGdmPort.SelectedIndex = (current != null && cbGdmPort.Items.Contains(current))
                    ? cbGdmPort.Items.IndexOf(current)
                    : 0;
            }
        }

        private int GetSelectedBaud()
        {
            int baud;
            return int.TryParse(cbGdmBaud.SelectedItem as string, out baud) ? baud : 115200;
        }

        private void btnGdmRefresh_Click(object sender, EventArgs e)
        {
            RefreshGdmPorts();
        }

        private void btnGdmConnect_Click(object sender, EventArgs e)
        {
            if (rbLan.Checked)
            {
                if (txtGdmIp.Text.Trim().Length == 0)
                {
                    MessageBox.Show(this, "請輸入 IP 位址。", "尚未輸入 IP",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                int port;
                if (!int.TryParse(txtGdmPort.Text.Trim(), out port) || port < 1 || port > 65535)
                {
                    MessageBox.Show(this, "Port 必須是 1~65535 的整數。", "Port 格式錯誤",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                if (cbGdmPort.SelectedItem == null)
                {
                    MessageBox.Show(this, "請先選擇 COM Port（按搜尋 COM Port 偵測）。", "尚未選擇 COM Port",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            SetConnLabel(lblConnGdm, "電表", ConnState.Connecting);
            lblConnGdm.Refresh(); // 連線(阻塞)前先讓「連線中/橘色」即時顯示

            bool lan = rbLan.Checked;
            bool ok = false;
            try
            {
                ApplyGdmConnectionSettings();
                _meter.Disconnect();   // 釋放任何舊連線，確保重連一定建立新的 TcpClient
                _meter.Connect();
                ok = _meter.IsConnected;
            }
            catch (Exception ex)
            {
                if (lan)
                    MessageBox.Show(this,
                        "無法重新連線到 GDM-8261A。\n\n" +
                        "請確認：\n" +
                        "1. LAN 線是否已接回\n" +
                        "2. IP / Port 是否正確\n" +
                        "3. 電表 LAN 功能是否啟用\n" +
                        "4. 等待 3 秒後再試一次",
                        "GDM LAN 連線失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                    MessageBox.Show(this, "電表連線失敗:\n" + ex.Message, "錯誤",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateConnStatus();

            if (ok)
            {
                _lanLostShown = false; // 連線成功 → 重置斷線提示旗標
                // 連線成功不跳 Popup，只寫 Debug Log（UI 已由 UpdateConnStatus 顯示綠色已連線）
                if (_debugLog != null)
                    _debugLog.Write(LogKind.Info, "GDM connected.");
            }
        }

        private void btnGdmDisconnect_Click(object sender, EventArgs e)
        {
            _meter.Disconnect();
            UpdateConnStatus();
        }

        private void btnRelayConnect_Click(object sender, EventArgs e)
        {
            SetConnLabel(lblConnRelay, "Relay", ConnState.Connecting);
            lblConnRelay.Refresh(); // 連線(阻塞)前先讓「連線中/橘色」即時顯示

            try
            {
                _relay.Connect();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Relay 連線失敗:\n" + ex.Message, "錯誤",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateConnStatus();
        }

        private void btnRelayDisconnect_Click(object sender, EventArgs e)
        {
            _relay.Disconnect();
            UpdateConnStatus();
        }

        private void UpdateConnStatus()
        {
            bool g = _meter.IsConnected;
            bool r = _relay.IsConnected;

            if (g)
            {
                // 多行顯示，避免被右側按鈕遮擋且資訊完整：已連線 / 連線資訊 / *IDN?
                string head = _meter.UseLan
                    ? "GDM8261A LAN " + _meter.Ip + ":" + _meter.TcpPort
                    : "GDM8261A Serial " + _meter.PortName + " " + _meter.BaudRate;
                string idn = string.IsNullOrEmpty(_meter.Idn) ? "" : _meter.Idn;
                lblGdmStatus.Text = "● 已連線" + Environment.NewLine + head +
                    (idn.Length > 0 ? Environment.NewLine + idn : "");
                lblGdmStatus.ForeColor = OkGreen;
            }
            else
            {
                lblGdmStatus.Text = "● 未連線";
                lblGdmStatus.ForeColor = Color.Red;
            }
            lblGdmIdn.Text = "";

            // Relay 三態：已連線（綠）/ 已偵測未連線（橘）/ 未偵測（紅）
            if (r)
            {
                lblRelayStatus.Text = "● USB Relay Connected";
                lblRelayStatus.ForeColor = OkGreen;
            }
            else if (_relayPresent)
            {
                lblRelayStatus.Text = "● USB Relay 已偵測，請按連線";
                lblRelayStatus.ForeColor = Color.Orange;
            }
            else
            {
                lblRelayStatus.Text = "● 未連線 (未偵測)";
                lblRelayStatus.ForeColor = Color.Red;
            }

            // Test 頁底部：電表 / Relay 狀態分開判斷、分別上色（已連線綠 / 未連線紅）
            SetConnLabel(lblConnGdm, "電表", g ? ConnState.Connected : ConnState.Disconnected);
            SetConnLabel(lblConnRelay, "Relay", r ? ConnState.Connected : ConnState.Disconnected);

            // 電表按鈕：已連線→[連線]停用/[中斷]啟用；未連線→[連線]啟用/[中斷]停用
            UpdateMeterConnectButton(g);
            btnGdmDisconnect.Enabled = g;

            // Relay 按鈕：未偵測→兩鈕停用；已偵測未連線→[連線]啟用/[中斷]停用；已連線→[連線]停用/[中斷]啟用
            if (r)
            {
                SetConnectButton(btnRelayConnect, true);
                btnRelayDisconnect.Enabled = true;
            }
            else if (_relayPresent)
            {
                SetConnectButton(btnRelayConnect, false);
                btnRelayDisconnect.Enabled = false;
            }
            else
            {
                SetConnectButton(btnRelayConnect, false);
                btnRelayConnect.Enabled = false; // 未偵測到 USB → 連線鈕也停用
                btnRelayDisconnect.Enabled = false;
            }

            // 已連線時鎖定連線參數（IP/Port 唯讀、LAN/Serial 與序列參數不可切換）
            UpdateConnectionFieldsLock(g);

            // 設備資訊區
            if (lblDevInfoGdm != null)
                lblDevInfoGdm.Text = "GDM Identify: " +
                    (g ? (string.IsNullOrEmpty(_meter.Idn) ? "(無回應)" : _meter.Idn) : "-");
            if (lblDevInfoRelay != null)
                lblDevInfoRelay.Text = "Relay VID/PID: 16C0:05DF  " + (r ? "(已連線)" : "(未連線)");

        }

        /// <summary>電表「連線」按鈕狀態：已連線→Disable/綠色/「已連線」；未連線→Enable/反灰/「連線」。</summary>
        private void UpdateMeterConnectButton(bool isConnected)
        {
            SetConnectButton(btnGdmConnect, isConnected);
        }

        /// <summary>Relay「連線」按鈕狀態：已連線→Disable/綠色/「已連線」；未連線→Enable/反灰/「連線」。</summary>
        private void UpdateRelayConnectButton(bool isConnected)
        {
            SetConnectButton(btnRelayConnect, isConnected);
        }

        /// <summary>
        /// 依電表連線狀態鎖定 / 解鎖連線參數欄位。
        /// 已連線：IP/Port 設為 ReadOnly（可看不可改、底色灰），LAN/Serial 與序列參數不可切換；
        /// 未連線：全部恢復可編輯 / 可切換。
        /// </summary>
        private void UpdateConnectionFieldsLock(bool meterConnected)
        {
            // 用 ReadOnly + 灰底（不直接 Enabled=false，避免字體變灰不易閱讀）
            txtGdmIp.ReadOnly = meterConnected;
            txtGdmPort.ReadOnly = meterConnected;
            txtGdmIp.BackColor = meterConnected ? SystemColors.Control : SystemColors.Window;
            txtGdmPort.BackColor = meterConnected ? SystemColors.Control : SystemColors.Window;

            // LAN / Serial 與序列參數：連線中不可切換
            rbLan.Enabled = !meterConnected;
            rbSerial.Enabled = !meterConnected;
            cbGdmPort.Enabled = !meterConnected;
            cbGdmBaud.Enabled = !meterConnected;
            btnGdmRefresh.Enabled = !meterConnected;
        }

        /// <summary>
        /// 統一管理「連線」按鈕外觀與可用狀態。
        /// 已連線時 Disable 並變綠，避免重複建立 TcpClient / SerialPort / Relay 連線。
        /// </summary>
        private static void SetConnectButton(Button button, bool isConnected)
        {
            if (button == null)
                return;

            // 統一風格：Flat、無黑色框線
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.UseVisualStyleBackColor = false; // 確保 BackColor 生效

            if (isConnected)
            {
                button.Enabled = false;
                button.BackColor = Color.FromArgb(76, 175, 80); // 綠底
                button.ForeColor = Color.White;                 // 白字
                button.Text = "已連線";
            }
            else
            {
                button.Enabled = true;
                button.BackColor = SystemColors.Control;        // 灰底
                button.ForeColor = Color.Black;                 // 黑字
                button.Text = "連線";
            }
        }

        #endregion

        #region 測試頁

        private void TxtBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true; // 避免 Enter 嗶聲
            if (_running)
                return;

            string sn = txtBarcode.Text.Trim();

            // 條碼/序號規則檢查（Config: barcodeRegex，空字串=不檢查）
            string pattern = AppSettings.Current.BarcodeRegex;
            if (!string.IsNullOrEmpty(pattern))
            {
                bool match;
                try { match = Regex.IsMatch(sn, pattern); }
                catch { match = true; } // 規則本身有誤時不阻擋

                if (!match)
                {
                    // 條碼格式錯誤：使用與「重覆測試確認」一致的原生 MessageBox（左側圖示 / 文字靠左 / 下方按鈕）。
                    ShowBarcodeMsg("✕ 條碼格式錯誤", Color.Firebrick, false);
                    MessageBox.Show(this,
                        "條碼/序號不符合規則。\n\n規則: " + pattern + "\n輸入: " + (sn.Length == 0 ? "(空白)" : sn),
                        "條碼格式錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ClearBarcodeMsg();   // 按確定後：清除右上角錯誤訊息與錯誤狀態
                    txtBarcode.Focus();  // 游標回到條碼/序號輸入框
                    return; // 不執行測試
                }
            }
            else if (sn.Length == 0)
            {
                ShowBarcodeMsg("✕ 請輸入序號", Color.Firebrick, false);
                return;
            }

            // 重覆條碼確認：若此條碼先前已完成測試，先詢問是否重測
            string prevResult;
            if (_testedBarcodes.TryGetValue(sn, out prevResult))
            {
                var dr = MessageBox.Show(this,
                    "條碼：" + sn + "\n\n已完成測試。\n\n結果：" + prevResult + "\n\n是否重新測試？",
                    "重覆測試確認", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (dr != DialogResult.OK)
                {
                    // 取消：停止流程，保留條碼方便操作
                    txtBarcode.SelectAll();
                    txtBarcode.Focus();
                    return;
                }
            }

            // 符合 → 顯示 OK（1 秒後消失）並自動進入測試流程
            ShowBarcodeMsg("✓ OK", OkGreen, true);
            StartTest(sn);
        }

        /// <summary>條碼欄位旁顯示訊息；autoHide=true 時 1 秒後自動清除。</summary>
        private void ShowBarcodeMsg(string text, Color color, bool autoHide)
        {
            lblBarcodeMsg.Text = text;
            lblBarcodeMsg.ForeColor = color;
            if (_okMsgTimer != null)
            {
                _okMsgTimer.Stop();
                if (autoHide)
                    _okMsgTimer.Start();
            }
        }

        /// <summary>清除右上角條碼錯誤訊息與相關錯誤狀態。</summary>
        private void ClearBarcodeMsg()
        {
            if (_okMsgTimer != null)
                _okMsgTimer.Stop();
            lblBarcodeMsg.Text = "";
        }

        /// <summary>顯示「LAN 連線中斷」提示（同一次斷線只跳一次，連線成功後重置）。</summary>
        private void NotifyLanDisconnected()
        {
            if (_lanLostShown)
                return;
            _lanLostShown = true;
            MessageBox.Show(this, "LAN 連線中斷，\n請確認網路線或儀器狀態。", "LAN 斷線",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private async void StartTest(string sn)
        {
            if (_running)
                return;

            if (!_meter.IsConnected)
            {
                MessageBox.Show(this, "電表未連線", "電表未連線",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tabMain.SelectedTab = tabDevice;
                return;
            }

            if (!_relay.IsConnected)
            {
                MessageBox.Show(this, "Relay未連線", "Relay未連線",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tabMain.SelectedTab = tabDevice;
                return;
            }

            if (string.IsNullOrEmpty(sn))
            {
                MessageBox.Show(this, "請先掃描或輸入條碼/序號。", "尚未輸入序號",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtBarcode.Focus();
                return;
            }

            _relayLostDuringTest = false;

            // 開始測試 → 立即清空條碼輸入框（原始條碼保留於序號列 / CSV / Debug Log）
            txtBarcode.Clear();

            // 重置畫面（Return to Step1），並固定新增「序號」列（粗體、不計入統計）
            dgvResults.Rows.Clear();
            AddSerialRow(sn);
            lblMeasure.Text = "---";
            lblRelay.Text = "--";
            lblCurrentStep.Text = "測試中";
            SetResult("測試中", Color.White, Color.RoyalBlue);
            SetRunningState(true);

            _cts = new CancellationTokenSource();

            TestResult result;
            try
            {
                result = await _flow.RunAsync(sn, _cts.Token);
            }
            catch (Exception ex)
            {
                lblInfo.Text = "測試發生例外: " + ex.Message;
                SetResult("NG", Color.White, NgRed);
                SetRunningState(false);
                UpdateConnStatus(); // 設備可能因例外失聯，更新狀態
                ResetLiveStatus();
                txtBarcode.Focus();
                return;
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
            }

            OnTestFinished(result);
        }

        /// <summary>主表格第一列固定新增「序號 | 條碼」（粗體、不參與 PASS/FAIL 統計與 FinalResult）。</summary>
        private void AddSerialRow(string barcode)
        {
            int idx = dgvResults.Rows.Add("序號", barcode, "", "", "", "", "");
            var row = dgvResults.Rows[idx];
            row.DefaultCellStyle.Font = new Font(dgvResults.Font, FontStyle.Bold);
            row.DefaultCellStyle.BackColor = Color.FromArgb(232, 240, 254);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(20, 40, 90);
        }

        private void OnTestFinished(TestResult result)
        {
            // 測試中 USB Relay 被拔除（背景監控取消流程）→ 視為 Relay 異常（而非單純中止）
            if (_relayLostDuringTest && !result.HasAnomaly)
            {
                result.Aborted = false;
                result.HasAnomaly = true;
                result.AnomalyType = DeviceAnomaly.RelayError;
                result.AnomalyMessage = "測試中偵測到 USB Relay 被拔除";
            }
            _relayLostDuringTest = false;

            // Step11 FinalResult：全流程跑完後才更新大型判定 Label
            if (result.Aborted)
                SetResult("中止", Color.White, Color.DarkOrange);
            else if (result.HasAnomaly)
                SetResult("NG", Color.White, NgRed);
            else if (result.IsPass)
                SetResult("PASS", Color.White, OkGreen);
            else
                SetResult("NG", Color.White, NgRed);

            string logFile = "";
            try
            {
                if (result.Steps.Count > 0)
                    logFile = CsvLogger.Append(result);
            }
            catch (Exception ex)
            {
                lblInfo.Text = "CSV 寫入失敗: " + ex.Message;
            }

            SetRunningState(false);
            UpdateSummaryStats(result, logFile); // 流程結束後才依最終 Step 結果更新統計

            // 記錄此條碼結果（供「重覆測試確認」）；中止不記錄
            if (!string.IsNullOrEmpty(result.SerialNumber) && !result.Aborted)
                _testedBarcodes[result.SerialNumber] = result.IsPass ? "OK" : "NG";

            // 設備異常：Popup 提示並修正 Relay 狀態
            if (result.HasAnomaly)
                HandleDeviceAnomaly(result);

            // Step12 Return Step1：恢復待測顯示，等待下一筆條碼
            ResetLiveStatus();
            txtBarcode.Focus();
        }

        /// <summary>設備異常處理：寫 Debug Log、修正 Relay 狀態、顯示「設備異常」Popup。</summary>
        private void HandleDeviceAnomaly(TestResult result)
        {
            if (_debugLog != null)
                _debugLog.Write(LogKind.Error,
                    "設備異常 Step" + result.AnomalyStep + " " + result.AnomalyStepName +
                    " | " + result.AnomalyType + ": " + result.AnomalyMessage);

            // Relay 相關異常（SetFeature/Write/Relay/Device Not Found）→ 立即標記 Relay 未連線
            bool relayIssue = DeviceAnomaly.IsRelayRelated(result.AnomalyType);
            if (relayIssue)
            {
                try { _relay.Disconnect(); }
                catch { }
            }
            UpdateConnStatus();

            if (relayIssue)
            {
                MessageBox.Show(this,
                    "偵測到 USB Relay 異常。\n\n" +
                    "可能原因：\n" +
                    "1. USB 已拔除\n" +
                    "2. Relay 板故障\n" +
                    "3. HID 通訊失敗\n\n" +
                    "請確認設備後重新測試。",
                    "Relay 異常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (_meter.UseLan)
            {
                // LAN 測試中斷線（section 4）
                MessageBox.Show(this,
                    "偵測到 GDM LAN 斷線。\n\n" +
                    "請確認：\n" +
                    "1. LAN 線是否接妥\n" +
                    "2. IP / Port 是否正確\n" +
                    "3. 電表 LAN 功能是否正常",
                    "設備異常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                MessageBox.Show(this,
                    "偵測到設備或網路異常\n\n" +
                    "異常類型：" + result.AnomalyType + "\n\n" +
                    "Step：" + result.AnomalyStep + " " + result.AnomalyStepName + "\n\n" +
                    "請確認：\n" +
                    "1. 電表電源\n" +
                    "2. LAN / COM\n" +
                    "3. Relay USB\n" +
                    "4. 設定參數",
                    "設備異常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Step12 Return Step1：恢復目前步驟=待測、Relay=--、量測值=---（保留結果與表格）。</summary>
        private void ResetLiveStatus()
        {
            lblCurrentStep.Text = "待測";
            lblRelay.Text = "--";
            lblRelay.ForeColor = Color.MediumBlue;
            lblMeasure.Text = "---";
        }

        // ===== 流程事件（皆於 UI 執行緒觸發） =====

        private void Flow_StepStarted(object sender, StepStartedEventArgs e)
        {
            lblCurrentStep.Text = "Step " + e.StepNumber + " — " + e.Description;
            lblInfo.Text = "執行中… Step " + e.StepNumber;
            if (_debugLog != null)
                _debugLog.Write(LogKind.Info, "Step " + e.StepNumber + " — " + e.Description);
        }

        private void Flow_RelayChanged(object sender, string code)
        {
            lblRelay.Text = code;
            lblRelay.ForeColor = Color.MediumBlue;
        }

        private void Flow_Measured(object sender, MeasurementEventArgs e)
        {
            lblMeasure.Text = TestStepResult.FormatValue(e.Value, e.Unit);
        }

        private void Flow_StepCompleted(object sender, TestStepResult e)
        {
            int index = dgvResults.Rows.Add(
                "Step" + e.StepNumber,
                e.StepName,
                e.RelayCode,
                e.Mode,
                e.ValueText,
                e.LimitText,
                e.Judgement);

            var row = dgvResults.Rows[index];
            if (e.IsInfo)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
                row.DefaultCellStyle.ForeColor = Color.DimGray;
            }
            else
            {
                row.DefaultCellStyle.BackColor = e.Pass
                    ? Color.FromArgb(232, 245, 233)
                    : Color.FromArgb(255, 235, 238);
                row.DefaultCellStyle.ForeColor = e.Pass
                    ? Color.FromArgb(27, 94, 32)
                    : Color.FromArgb(183, 28, 28);
            }

            // 主表格只顯示一列，但有複測時把每次嘗試明細寫入 Debug Log（Retry 0 / 1 / 2）
            if (_debugLog != null && e.Attempts != null && e.Attempts.Count > 1)
            {
                foreach (var a in e.Attempts)
                    _debugLog.Write(a.Pass ? LogKind.Info : LogKind.Error,
                        string.Format("    Step{0} {1} Retry {2}: {3} | 判定 {4} → {5}",
                            a.StepNumber, a.StepName, a.Attempt,
                            TestStepResult.FormatValue(a.Value, a.Unit),
                            e.LimitText, a.Pass ? "PASS" : "NG"));
            }

            dgvResults.FirstDisplayedScrollingRowIndex = index;
        }

        private void Flow_StatusChanged(object sender, string status)
        {
            lblInfo.Text = status;
        }

        #endregion

        private void SetResult(string text, Color foreColor, Color backColor)
        {
            lblResult.Text = text;
            lblResult.ForeColor = foreColor;
            lblResult.BackColor = backColor;
        }

        private void SetRunningState(bool running)
        {
            _running = running;
            txtBarcode.Enabled = !running;
            tabDevice.Enabled = !running; // 測試中不可改設備設定
        }

        /// <summary>
        /// 流程結束後才呼叫：依「主表格中每個 Step 的最終結果」統計 PASS / FAIL。
        /// 規則：序號列不在 result.Steps 中（不統計）；其餘每個 Step 只算一次（已整併，Retry 不重複）。
        /// PASS / OK 皆算 PASS；NG / NG (Retry n) / 設備異常 皆算 FAIL。
        /// 注意：FinalResult（result.IsPass）仍只依量測步驟，資訊步驟雖計入 PASS 數但不影響 FinalResult。
        /// </summary>
        private void UpdateSummaryStats(TestResult result, string logFile = null)
        {
            string text;
            if (result == null)
            {
                text = "就緒";
            }
            else
            {
                int pass = 0, fail = 0;
                foreach (var s in result.Steps)
                {
                    // Step1/2/6 (OK) 與 Step3/4/5/7/8/9/10 (PASS) → PASS；NG / 設備異常 → FAIL
                    if (s.Pass) pass++;
                    else fail++;
                }
                int total = pass + fail;

                string verdict = result.HasAnomaly ? "設備異常" : result.Judgement;
                text = string.Format("總Step數: {0}    PASS: {1}    FAIL: {2}    最後結果: {3}",
                    total, pass, fail, verdict);

                if (!result.IsPass && !result.Aborted && result.FirstFailedStep != null)
                    text += " (Step " + result.FirstFailedStep.StepNumber + " " + result.FirstFailedStep.StepName + ")";
            }

            if (!string.IsNullOrEmpty(logFile))
                text += "    紀錄: " + logFile;

            lblInfo.Text = text;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_heartbeatTimer != null)
            {
                _heartbeatTimer.Stop();
                _heartbeatTimer.Dispose();
                _heartbeatTimer = null;
            }

            if (_relayMonitorTimer != null)
            {
                _relayMonitorTimer.Stop();
                _relayMonitorTimer.Dispose();
                _relayMonitorTimer = null;
            }

            if (_logFlushTimer != null)
            {
                _logFlushTimer.Stop();
                _logFlushTimer.Dispose();
                _logFlushTimer = null;
            }

            if (_cts != null)
            {
                try { _cts.Cancel(); } catch { }
            }

            if (_relay != null) _relay.Disconnect();
            if (_meter != null) _meter.Disconnect();

            base.OnFormClosing(e);
        }
    }
}
