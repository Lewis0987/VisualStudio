using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.ComponentModel;
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

        // V2.1：測試控制（暫停 / 停止）；btnPause/btnStop 於 BuildBarcodeArea 建立
        private Button btnPause;
        private Button btnStop;
        private bool _userStopped;

        // V2.2：員工登入 / 權限；btnLogout 於 BuildBarcodeArea 建立（登入改為自動觸發，無登入按鈕）
        private OperatorAuth _auth;
        private Button btnLogout;

        // 已測試過的條碼 → 結果（OK / NG），供「重覆測試確認」使用（整個 session 保留）
        private readonly Dictionary<string, string> _testedBarcodes = new Dictionary<string, string>();

        // 避免同一次斷線重複跳出「LAN 連線中斷」提示；連線成功後重置
        private bool _lanLostShown;

        // V2.2：曾發生 LAN 中途斷線（供下次連線成功時記錄 Reconnect Success）
        private bool _lanWasLost;

        // USB Relay 是否被偵測到（由背景監控更新；不代表已連線）
        private bool _relayPresent;
        private bool _relayConnectedLast;
        // 測試中偵測到 USB Relay 被拔除（用於結束後顯示 Relay 異常）
        private bool _relayLostDuringTest;

        private static readonly Color OkGreen = Color.FromArgb(46, 160, 67);
        private static readonly Color NgRed = Color.FromArgb(211, 47, 47);

        /// <summary>程式版本號（顯示於視窗標題與狀態列）。</summary>
        public const string Version = "V2.2";

        /// <summary>Power ON 檢查門檻：量測電壓 &gt;= 此值視為已開機。</summary>
        private const double PowerOnMinVoltage = 1.0;

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

        // USB Relay 背景監控：每 1 秒偵測 VID/PID 插拔，自動連線 / 斷線
        private System.Windows.Forms.Timer _relayMonitorTimer;

        // V2.2：LAN 背景連線監控（每 GdmMonitorIntervalMs 送一次 *IDN? 偵測連線存活）
        private System.Windows.Forms.Timer _heartbeatTimer;
        private bool _heartbeatBusy;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            AppSettings.Load();

            // V2.2：載入員工帳號（首次執行自動建立預設 Admin 00000000）
            _auth = new OperatorAuth();
            _auth.Load();

            // 鮑率選項
            cbGdmBaud.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });

            InitDevices();
            BuildDebugUi();
            RefreshGdmPorts();

            SetRunningState(false);
            SetResult("待測", Color.DimGray, Color.Gainsboro);

            BuildBarcodeArea();
            txtBarcode.KeyDown += TxtBarcode_KeyDown;
            txtBarcode.Enter += (s, ev) => txtBarcode.SelectAll();
            txtBarcode.TextChanged += (s, ev) => { if (_barcodeError) SetBarcodeError(false); else UpdateBarcodeHint(); };

            // 自動聚焦：啟動完成、切回 Test 頁
            this.Shown += (s, ev) => FocusBarcode();
            tabMain.SelectedIndexChanged += (s, ev) => { if (tabMain.SelectedTab == tabTest) FocusBarcode(); };
            // V2.2：未登入時切到 Settings / Debug Log 先驗證；取消則留在原頁
            tabMain.Selecting += TabMain_Selecting;

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

            // V2.1：版本號（視窗標題 + 狀態列）
            this.Text = "DX01 外殼短路流程測試 " + Version;
            lblVersion.Text = "Version : " + Version;

            // V2.1：右下角可收合的 USB Relay 控制小視窗
            BuildRelayPanel();
            UpdateRelayPanelState();

            // V2.2：依登入狀態套用權限 / Operator 顯示
            UpdatePermissionsUi();

            // V2.2：LAN 背景連線監控（待機時每秒送 *IDN? 偵測斷線；非同步執行避免 UI 卡頓）
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
        /// LAN 背景連線監控：待機時每隔 GdmMonitorIntervalMs 送一次 *IDN? 偵測連線存活。
        /// 在背景執行緒執行（避免 UI 卡頓）；失敗時 PingDevice 內 DropConnection → ConnectionLost
        /// → OnGdmConnectionLost 即時更新「電表：未連線」+ Debug Log。測試中不介入（流程自行偵測）。
        /// 不自動重連，需使用者手動按「連線」。
        /// </summary>
        private async void Heartbeat_Tick(object sender, EventArgs e)
        {
            if (_running || _heartbeatBusy) return;
            if (_meter == null || !_meter.UseLan || !_meter.IsConnected) return;

            _heartbeatBusy = true;
            try
            {
                await Task.Run(() => _meter.PingDevice());
            }
            catch { /* PingDevice 內已處理例外 */ }
            finally
            {
                _heartbeatBusy = false;
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

        // ===== 條碼欄位：自訂 Placeholder（小字/淡灰）+ 錯誤紅框 + 下方錯誤訊息 =====
        private const string BarcodePlaceholderText = "Please enter the barcode.";
        private static readonly Color BarcodePlaceholderColor = Color.DimGray;  // 深灰，易閱讀
        private static readonly Color BarcodeBorderNormal = Color.White;  // 正常邊框=白(不可見)，錯誤=紅
        private Panel _barcodeBox;             // 輸入框外框（BackColor 當邊框：正常白 / 錯誤紅）
        private bool _barcodeError;            // 是否為格式錯誤狀態

        /// <summary>重組條碼區：caption | [輸入框(可紅框) + 下方錯誤訊息]。</summary>
        private void BuildBarcodeArea()
        {
            panelTop.SuspendLayout();
            panelTop.Controls.Clear();
            panelTop.ColumnStyles.Clear();
            panelTop.RowStyles.Clear();

            // 先設定輸入框，取得實際高度（內層恰好等於文字高，避免底色外露 / 邊框不完整）
            txtBarcode.BorderStyle = BorderStyle.None;
            txtBarcode.BackColor = Color.White;
            txtBarcode.Dock = DockStyle.Fill;
            txtBarcode.Margin = Padding.Empty;
            int boxH = txtBarcode.PreferredHeight + 4;          // 上下各 2px 邊框

            panelTop.ColumnCount = 3;
            panelTop.RowCount = 2;
            panelTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));          // 標題
            panelTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));     // 輸入框
            panelTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));          // 暫停 / 停止
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, boxH));          // 第0列：標題 + 輸入框
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));           // 第1列：提示 / 錯誤（緊鄰輸入框下方）

            // 標題固定於第 0 列、垂直置中
            lblBarcodeCaption.AutoSize = true;
            lblBarcodeCaption.Anchor = AnchorStyles.Left;
            panelTop.Controls.Add(lblBarcodeCaption, 0, 0);

            // 輸入框外框（BackColor 當邊框）：Dock=Fill + Margin 0 + Padding 2 → 四邊 2px 完整（含右側）
            _barcodeBox = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(2),
                BackColor = BarcodeBorderNormal
            };
            _barcodeBox.Controls.Add(txtBarcode);
            panelTop.Controls.Add(_barcodeBox, 1, 0);

            // V2.1：條碼輸入框右側的測試控制按鈕（暫停 / 停止）
            var ctrlPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(8, 0, 0, 0),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            // V2.2：登出（左，登入時才顯示）＋ 測試控制 暫停 / 停止（右）。登入改為自動觸發，無登入按鈕。
            btnLogout = new Button { Text = "登出", Size = new Size(64, boxH), Visible = false, Margin = new Padding(0, 0, 12, 0) };
            btnLogout.Click += BtnLogout_Click;
            btnPause = new Button { Text = "暫停", Size = new Size(72, boxH), Enabled = false, Margin = new Padding(0, 0, 4, 0) };
            btnStop = new Button { Text = "停止", Size = new Size(72, boxH), Enabled = false, Margin = new Padding(0) };
            btnPause.Click += BtnPause_Click;
            btnStop.Click += BtnStop_Click;
            ctrlPanel.Controls.Add(btnLogout);
            ctrlPanel.Controls.Add(btnPause);
            ctrlPanel.Controls.Add(btnStop);
            panelTop.Controls.Add(ctrlPanel, 2, 0);

            // 提示 / 錯誤：第 1 列、靠左對齊輸入框、緊鄰下方（上邊距約 3px）
            lblBarcodeMsg.AutoSize = false;
            lblBarcodeMsg.Dock = DockStyle.Fill;
            lblBarcodeMsg.TextAlign = ContentAlignment.TopLeft;
            lblBarcodeMsg.Padding = new Padding(1, 3, 0, 0);    // 左對齊框邊、距輸入框約 3px
            lblBarcodeMsg.BackColor = Color.Transparent;
            lblBarcodeMsg.Font = new Font("Microsoft JhengHei UI", 8.25F);
            lblBarcodeMsg.Text = "";
            lblBarcodeMsg.Visible = false;
            panelTop.Controls.Add(lblBarcodeMsg, 1, 1);

            panelTop.ResumeLayout();

            UpdateBarcodeHint();
        }

        /// <summary>
        /// 更新輸入框下方提示（優先序：錯誤 &gt; placeholder &gt; 無）。
        /// 不疊在 TextBox 內，故不影響游標顯示。
        /// </summary>
        private void UpdateBarcodeHint()
        {
            if (_barcodeError)
            {
                lblBarcodeMsg.Text = "Barcode format invalid.";
                lblBarcodeMsg.ForeColor = Color.Red;
                lblBarcodeMsg.Visible = true;
            }
            else if (txtBarcode.Text.Length == 0)
            {
                lblBarcodeMsg.Text = BarcodePlaceholderText;        // Please enter the barcode.
                lblBarcodeMsg.ForeColor = BarcodePlaceholderColor;  // LightGray
                lblBarcodeMsg.Visible = true;
            }
            else
            {
                lblBarcodeMsg.Text = "";
                lblBarcodeMsg.Visible = false;
            }
        }

        /// <summary>聚焦條碼輸入框（方便直接掃下一顆）。</summary>
        private void FocusBarcode()
        {
            if (txtBarcode != null && txtBarcode.CanFocus)
            {
                txtBarcode.Focus();
                txtBarcode.SelectAll();
            }
        }

        /// <summary>格式錯誤 → 紅框 + 下方紅字訊息；false → 還原正常樣式並隱藏訊息。</summary>
        private void SetBarcodeError(bool on)
        {
            _barcodeError = on;
            if (_barcodeBox != null)
                _barcodeBox.BackColor = on ? Color.Red : BarcodeBorderNormal;  // 1px 紅框 / 還原白框
            UpdateBarcodeHint();
        }

        /// <summary>建立真實設備控制器與測試流程。</summary>
        private void InitDevices()
        {
            _relay = new RealRelayController();
            _meter = new RealGdm8261AController();

            // 集中：任何 Relay 切換（連線復位 / 設備測試 / 流程）都同步更新 UI
            _relay.RelayChanged += OnRelayChanged;

            // V2.2：電表通訊中途斷線 → 即時更新 UI 並寫 Debug Log（比照 GDM8261A_Tester2 DropConnection）
            _meter.ConnectionLost += OnGdmConnectionLost;

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
            // V2.2：取消 Settings 權限限制 — 未登入 / Operator / Admin 皆可查看與修改 / 儲存
            AppSettings before = AppSettings.Current.Clone();   // 開啟前快照，供儲存後比對差異
            DialogResult dr;
            using (var f = new SettingForm())
            {
                dr = f.ShowDialog(this);
            }
            if (dr == DialogResult.OK)
                LogSettingsDiff(before, AppSettings.Current);   // 有按儲存才比對 / 寫 Log

            LoadConnectionUiFromSettings();
            UpdateGdmInterfaceUi();
            ApplyUiSettings();
            UpdateConnStatus();
        }

        /// <summary>儲存後比對設定差異並寫入 Debug Log（僅記變更項；含修改者；不含任何密碼）。</summary>
        private void LogSettingsDiff(AppSettings o, AppSettings n)
        {
            if (_debugLog == null || o == null || n == null)
                return;

            var lines = new List<string>();
            foreach (FieldInfo f in typeof(AppSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object ov = f.GetValue(o), nv = f.GetValue(n);

                if (f.FieldType == typeof(int[]))   // StepWaitMs[1..10]
                {
                    var a = ov as int[];
                    var b = nv as int[];
                    if (a != null && b != null)
                    {
                        int len = Math.Min(a.Length, b.Length);
                        for (int i = 1; i < len; i++)
                            if (a[i] != b[i])
                                lines.Add("Step" + i + "WaitMs : " + a[i] + " -> " + b[i]);
                    }
                    continue;
                }

                if (!object.Equals(ov, nv))
                    lines.Add(FriendlySettingName(f.Name) + " : " +
                              FmtSetting(f.Name, ov) + " -> " + FmtSetting(f.Name, nv));
            }

            if (lines.Count == 0)
                return;   // 舊值 = 新值 → 不寫入

            _debugLog.Write(LogKind.Info, "[Settings Changed]");
            _debugLog.Write(LogKind.Info, "User : " + SettingsActor());
            foreach (string l in lines)
                _debugLog.Write(LogKind.Info, l);
        }

        /// <summary>修改者顯示：Admin 00000000 / OP 11506023 / 未登入。</summary>
        private string SettingsActor()
        {
            if (_auth != null && _auth.IsLoggedIn)
                return (_auth.IsAdmin ? "Admin " : "OP ") + _auth.OperatorId;
            return "未登入";
        }

        private static string FriendlySettingName(string name)
        {
            switch (name)
            {
                case "DcVoltageRange": return "DC Voltage Range";
                case "BarcodeRegex": return "Barcode Regex";
                case "RelaySwitchDelayMs": return "RelaySwitchDelayMs";
                case "ReconnectRetryCount": return "RetryCount";
                default: return name;
            }
        }

        private static string FmtSetting(string name, object v)
        {
            if (v == null) return "";
            if (name == "DcVoltageRange") return v + "V";
            return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
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
                    MsgBox.Show(this, "尚未輸入 IP", "請輸入 IP 位址。", MessageBoxIcon.Warning, "確定");
                    return;
                }
                int port;
                if (!int.TryParse(txtGdmPort.Text.Trim(), out port) || port < 1 || port > 65535)
                {
                    MsgBox.Show(this, "Port 格式錯誤", "Port 必須是 1~65535 的整數。", MessageBoxIcon.Warning, "確定");
                    return;
                }
            }
            else
            {
                if (cbGdmPort.SelectedItem == null)
                {
                    MsgBox.Show(this, "尚未選擇 COM Port", "請先選擇 COM Port（按搜尋 COM Port 偵測）。", MessageBoxIcon.Warning, "確定");
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
                if (lan && _debugLog != null)
                    _debugLog.Write(LogKind.Error, "Reconnect Failed : " + ex.Message);

                if (lan)
                    MsgBox.Show(this, "GDM LAN 連線失敗",
                        "無法重新連線到 GDM-8261A。\n\n" +
                        "請確認：\n" +
                        "1. LAN 線是否已接回\n" +
                        "2. IP / Port 是否正確\n" +
                        "3. 電表 LAN 功能是否啟用\n" +
                        "4. 等待 3 秒後再試一次\n" +
                        "5. 若仍無法連線，請重新開啟 GDM-8261A 設備後再重新連線\n\n" +
                        "可能原因：\n" +
                        "- 網路連線異常\n" +
                        "- 電表 TCP 通訊未正常釋放\n" +
                        "- 設備端通訊狀態卡住",
                        MessageBoxIcon.Error, "確定");
                else
                    MsgBox.Show(this, "錯誤", "電表連線失敗:\n" + ex.Message, MessageBoxIcon.Error, "確定");
            }

            UpdateConnStatus();

            if (ok)
            {
                _lanLostShown = false; // 連線成功 → 重置斷線提示旗標
                // 連線成功不跳 Popup，只寫 Debug Log（UI 已由 UpdateConnStatus 顯示綠色已連線）
                if (_debugLog != null)
                    _debugLog.Write(LogKind.Info, _lanWasLost ? "Reconnect Success" : "LAN Connected");
                _lanWasLost = false;
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
                MsgBox.Show(this, "錯誤", "Relay 連線失敗:\n" + ex.Message, MessageBoxIcon.Error, "確定");
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

            UpdateRelayPanelState();   // 同步右下角 Relay 小視窗的可用狀態 / 燈號
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

            // 空白：不開始測試（placeholder 已提示）
            if (sn.Length == 0)
            {
                SetBarcodeError(false);
                return;
            }

            // 條碼/序號規則檢查（Config: barcodeRegex，空字串=不檢查）
            string pattern = AppSettings.Current.BarcodeRegex;
            if (!string.IsNullOrEmpty(pattern))
            {
                bool match;
                try { match = Regex.IsMatch(sn, pattern); }
                catch { match = true; } // 規則本身有誤時不阻擋

                if (!match)
                {
                    // 格式錯誤：輸入框紅框 + 框下方紅字「Barcode format invalid.」（不跳 Popup）
                    SetBarcodeError(true);
                    txtBarcode.Focus();
                    txtBarcode.SelectAll();
                    return; // 不執行測試
                }
            }

            // 重覆條碼確認：若此條碼先前已完成測試，先詢問是否重測
            string prevResult;
            if (_testedBarcodes.TryGetValue(sn, out prevResult))
            {
                // 1 = 重新測試；0 = 取消（或關閉視窗 -1）
                int dr = MsgBox.Show(this, "重覆測試確認",
                    "條碼：" + sn + "\n\n已完成測試。\n\n結果：" + prevResult + "\n\n是否重新測試？",
                    MessageBoxIcon.Question, "取消", "重新測試");
                if (dr != 1)
                {
                    // 取消：停止流程，保留條碼方便操作
                    txtBarcode.SelectAll();
                    txtBarcode.Focus();
                    return;
                }
            }

            // 符合格式 → 還原正常樣式並自動進入測試流程
            SetBarcodeError(false);
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
            MsgBox.Show(this, "LAN 斷線", "LAN 連線中斷，\n請確認網路線或儀器狀態。", MessageBoxIcon.Warning, "確定");
        }

        /// <summary>測試控制按鈕（暫停 / 停止）啟用狀態；停用時將「暫停」文字復位。</summary>
        private void SetTestControlsEnabled(bool running)
        {
            if (btnPause == null || btnStop == null) return;
            btnPause.Enabled = running;
            btnStop.Enabled = running;
            if (!running) btnPause.Text = "暫停";
        }

        private void BtnPause_Click(object sender, EventArgs e)
        {
            if (!_running) return;
            if (!_flow.IsPaused)
            {
                _flow.Pause();
                btnPause.Text = "繼續";
                lblCurrentStep.Text = "暫停中";
                if (_debugLog != null) _debugLog.Write(LogKind.Info, "測試已暫停（暫停中）");
            }
            else
            {
                _flow.Resume();
                btnPause.Text = "暫停";
                if (_debugLog != null) _debugLog.Write(LogKind.Info, "測試已繼續");
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (!_running) return;
            _userStopped = true;
            if (_debugLog != null) _debugLog.Write(LogKind.Error, "測試已由使用者停止");
            // 取消令會解除暫停等待（WaitWhilePausedAsync 內已註冊 token），流程隨即中止
            try { if (_cts != null) _cts.Cancel(); } catch { }
            SetTestControlsEnabled(false);
        }

        /// <summary>PASS：清空條碼、Focus（顯示 placeholder），等待掃下一顆。</summary>
        private void ClearBarcodeForNext()
        {
            txtBarcode.Clear();   // 觸發 TextChanged → 顯示 placeholder
            txtBarcode.Focus();
        }

        /// <summary>NG / 異常 / 停止 / 未開機：保留原條碼、Focus、全選，方便直接重測或人工確認。</summary>
        private void KeepBarcodeForRetry(string sn)
        {
            txtBarcode.Text = sn ?? "";
            txtBarcode.Focus();
            txtBarcode.SelectAll();
        }

        /// <summary>
        /// 「產品未開機」彈窗（兩按鈕）。
        /// 回傳 true = 略過（忽略警告繼續測試）；false = 確定（停止測試）。
        /// 自訂對話框以支援按鈕文字 / 水平靠右排列（MessageBox 無法自訂）。
        /// </summary>
        private bool ShowPowerOffPrompt(double voltage)
        {
            string msg =
                "偵測到產品電壓過低。\n" +
                "目前量測值：" + voltage.ToString("0.000") + " V\n\n" +
                "「確定」：停止測試，確認產品開機後重測。\n" +
                "「略過」：忽略此警告並繼續測試。";
            // 0 = 略過（繼續），1 = 確定（停止）；Enter / 關閉 → 確定（安全停止）
            return MsgBox.Show(this, "產品未開機", msg, MessageBoxIcon.Warning, "略過", "確定") == 0;
        }

        private async void StartTest(string sn)
        {
            if (_running)
                return;

            // V2.2：未登入禁止測試 → 先跳登入視窗；仍未登入則中止
            if (!_auth.IsLoggedIn)
            {
                if (!PromptLogin())
                    return;
            }

            if (!_meter.IsConnected)
            {
                MsgBox.Show(this, "電表未連線", "電表尚未連線，請先連線 GDM-8261A。", MessageBoxIcon.Warning, "確定");
                tabMain.SelectedTab = tabDevice;
                return;
            }

            if (!_relay.IsConnected)
            {
                MsgBox.Show(this, "Relay 未連線", "USB Relay 尚未連線，請先連線 Relay。", MessageBoxIcon.Warning, "確定");
                tabMain.SelectedTab = tabDevice;
                return;
            }

            if (string.IsNullOrEmpty(sn))
            {
                MsgBox.Show(this, "尚未輸入序號", "請先掃描或輸入條碼 / 序號。", MessageBoxIcon.Warning, "確定");
                txtBarcode.Focus();
                return;
            }

            _relayLostDuringTest = false;

            // 開始測試 → 立即清空條碼輸入框（原始條碼保留於序號列 / CSV / Debug Log）
            txtBarcode.Clear();

            // V2.2：Debug Log 記錄操作者 / 條碼 / 開始測試（不記錄密碼）
            if (_debugLog != null)
            {
                _debugLog.Write(LogKind.Info, "Operator : " + _auth.OperatorId);
                _debugLog.Write(LogKind.Info, "Barcode : " + sn);
                _debugLog.Write(LogKind.Info, "Start Test");
            }

            // ── 測試開始前：產品 Power ON 檢查（避免到 Step7 才發現產品未開機）──
            SetRunningState(true);
            lblCurrentStep.Text = "Power ON 檢查";
            SetResult("檢查中", Color.White, Color.RoyalBlue);

            _cts = new CancellationTokenSource();
            PowerCheckResult power;
            try
            {
                power = await _flow.CheckPowerOnAsync(PowerOnMinVoltage, _cts.Token);
            }
            catch (Exception ex)
            {
                power = new PowerCheckResult { HasAnomaly = true, AnomalyType = "Device Error", AnomalyMessage = ex.Message };
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
            }

            // 設備 / 通訊異常：立即停止、提示，Final Result = NG（不進入 Step1）
            if (power.HasAnomaly)
            {
                SetResult("NG", Color.White, NgRed);
                SetRunningState(false);
                HandleDeviceAnomaly(new TestResult
                {
                    HasAnomaly = true,
                    AnomalyType = power.AnomalyType,
                    AnomalyMessage = power.AnomalyMessage,
                    AnomalyStep = 0,
                    AnomalyStepName = "Power ON 檢查"
                });
                ResetLiveStatus();
                KeepBarcodeForRetry(sn);   // 視同 NG：保留條碼 + Focus + SelectAll
                return;
            }

            // 產品未開機（電壓過低）：不進入 Step1、不建立測試結果，提示後 Focus 回條碼
            if (!power.PowerOn)
            {
                if (_debugLog != null)
                    _debugLog.Write(LogKind.Error,
                        "Power OFF：量測 " + power.Voltage.ToString("0.000") + " V < " +
                        PowerOnMinVoltage.ToString("0.###") + " V");

                bool skip = ShowPowerOffPrompt(power.Voltage);
                if (!skip)
                {
                    // 確定：停止測試、保留條碼、回待測（視同 NG）
                    if (_debugLog != null) _debugLog.Write(LogKind.Info, "Power OFF：使用者選擇「確定」→ 停止測試");
                    SetResult("待測", Color.DimGray, Color.Gainsboro);
                    SetRunningState(false);
                    ResetLiveStatus();
                    KeepBarcodeForRetry(sn);
                    return;
                }

                // 略過：忽略警告、不停止，繼續往下執行 Step1~Step10
                if (_debugLog != null) _debugLog.Write(LogKind.Info, "Power OFF：使用者選擇「略過」→ 繼續測試");
            }

            // ── Power ON → 進入正式測試 ──
            // 重置畫面（Return to Step1），固定新增「序號」列（工號不顯示於表格，僅記於 Debug Log / 底部）
            dgvResults.Rows.Clear();
            AddSerialRow(sn);
            lblMeasure.Text = "---";
            lblRelay.Text = "--";
            lblCurrentStep.Text = "測試中";
            SetResult("測試中", Color.White, Color.RoyalBlue);

            _cts = new CancellationTokenSource();
            _userStopped = false;
            SetTestControlsEnabled(true);   // 進入正式測試 → 暫停 / 停止可用

            TestResult result;
            try
            {
                result = await _flow.RunAsync(sn, _cts.Token);
            }
            catch (Exception ex)
            {
                lblInfo.Text = "測試發生例外: " + ex.Message;
                SetResult("NG", Color.White, NgRed);
                SetTestControlsEnabled(false);
                SetRunningState(false);
                UpdateConnStatus(); // 設備可能因例外失聯，更新狀態
                ResetLiveStatus();
                KeepBarcodeForRetry(sn);   // 視同 NG：保留條碼 + Focus + SelectAll
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
            // V2.2：紀錄操作者工號於結果（人員追溯；不含密碼）
            if (string.IsNullOrEmpty(result.OperatorId) && _auth != null)
                result.OperatorId = _auth.OperatorId;

            // 測試中 USB Relay 被拔除（背景監控取消流程）→ 視為 Relay 異常（而非單純中止）
            if (_relayLostDuringTest && !result.HasAnomaly)
            {
                result.Aborted = false;
                result.HasAnomaly = true;
                result.AnomalyType = DeviceAnomaly.RelayError;
                result.AnomalyMessage = "測試中偵測到 USB Relay 被拔除";
            }
            _relayLostDuringTest = false;

            // V2.1：測試結束 → 停用暫停 / 停止
            SetTestControlsEnabled(false);

            // Step11 FinalResult：全流程跑完後才更新大型判定 Label
            if (result.Aborted)
                SetResult(_userStopped ? "停止" : "中止", Color.White, Color.DarkOrange);
            else if (result.HasAnomaly)
                SetResult("NG", Color.White, NgRed);
            else if (result.IsPass)
                SetResult("PASS", Color.White, OkGreen);
            else
                SetResult("NG", Color.White, NgRed);

            // V2.2：Debug Log 記錄操作者與最終結果
            if (_debugLog != null)
            {
                string verdict = result.Aborted ? (_userStopped ? "STOP" : "ABORT")
                    : (result.HasAnomaly ? "NG (設備異常)" : (result.IsPass ? "PASS" : "NG"));
                _debugLog.Write(LogKind.Info, "Operator : " + result.OperatorId);
                _debugLog.Write(LogKind.Info, "Result : " + verdict);
            }

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

            // V2.1 條碼行為：PASS → 清空等待下一顆；NG / 異常 / 停止 → 保留原條碼 + 全選方便重測
            bool pass = !result.Aborted && !result.HasAnomaly && result.IsPass;
            if (pass)
                ClearBarcodeForNext();
            else
                KeepBarcodeForRetry(result.SerialNumber);
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
                MsgBox.Show(this, "Relay 異常",
                    "偵測到 USB Relay 異常。\n\n" +
                    "可能原因：\n" +
                    "1. USB 已拔除\n" +
                    "2. Relay 板故障\n" +
                    "3. HID 通訊失敗\n\n" +
                    "請確認設備後重新測試。",
                    MessageBoxIcon.Error, "確定");
            }
            else if (_meter.UseLan)
            {
                // LAN 測試中斷線
                MsgBox.Show(this, "設備異常",
                    "偵測到 GDM LAN 斷線。\n\n" +
                    "請確認：\n" +
                    "1. LAN 線是否接妥\n" +
                    "2. IP / Port 是否正確\n" +
                    "3. 電表 LAN 功能是否正常",
                    MessageBoxIcon.Error, "確定");
            }
            else
            {
                MsgBox.Show(this, "設備異常",
                    "偵測到設備或網路異常\n\n" +
                    "異常類型：" + result.AnomalyType + "\n\n" +
                    "Step：" + result.AnomalyStep + " " + result.AnomalyStepName + "\n\n" +
                    "請確認：\n" +
                    "1. 電表電源\n" +
                    "2. LAN / COM\n" +
                    "3. Relay USB\n" +
                    "4. 設定參數",
                    MessageBoxIcon.Error, "確定");
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
            // 目前步驟區：只顯示正式步驟名稱（去掉 " Retry N" 與 Range，避免過長換行）
            string baseName = e.Description;
            int retryIdx = baseName.IndexOf(" Retry ", StringComparison.Ordinal);
            if (retryIdx >= 0)
                baseName = baseName.Substring(0, retryIdx);
            lblCurrentStep.Text = "Step " + e.StepNumber + " — " + baseName;

            lblInfo.Text = "執行中… Step " + e.StepNumber;
            // Debug Log 保留完整資訊（含 Retry）
            if (_debugLog != null)
                _debugLog.Write(LogKind.Info, "Step " + e.StepNumber + " — " + e.Description);
        }

        private void Flow_RelayChanged(object sender, string code)
        {
            UpdateRelayDisplay(code);
        }

        /// <summary>Relay 控制器事件（任何 SetRelay 都會觸發）；可能在背景執行緒，需 Invoke。</summary>
        private void OnRelayChanged(object sender, string code)
        {
            UpdateRelayDisplay(code);
            UpdateRelayPanelVisual(code);   // 同步右下角 Relay 小視窗燈號
        }

        /// <summary>電表通訊中途斷線（任何 I/O 失敗）：即時更新 UI 為未連線並寫 Debug Log。可能在背景執行緒。</summary>
        private void OnGdmConnectionLost(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => OnGdmConnectionLost(sender, e))); } catch { }
                return;
            }

            _lanWasLost = true;
            if (_debugLog != null)
            {
                _debugLog.Write(LogKind.Error, "[LAN Disconnect] GDM8261A Connection Lost");
                _debugLog.Write(LogKind.Error, "LAN Disconnected");
            }
            UpdateConnStatus();   // 立即更新「電表：未連線」（紅）
        }

        /// <summary>同步 Test 頁「Relay 狀態」顯示（執行緒安全）。</summary>
        private void UpdateRelayDisplay(string code)
        {
            if (lblRelay.InvokeRequired)
            {
                lblRelay.BeginInvoke(new Action(() => UpdateRelayDisplay(code)));
                return;
            }
            lblRelay.Text = code;
            lblRelay.ForeColor = Color.MediumBlue;
        }

        private void Flow_Measured(object sender, MeasurementEventArgs e)
        {
            lblMeasure.Text = TestStepResult.FormatMeasureValue(e.Value, e.Unit);
        }

        private void Flow_StepCompleted(object sender, TestStepResult e)
        {
            int index = dgvResults.Rows.Add(
                "Step" + e.StepNumber,
                e.StepName,
                e.RelayCode,
                e.Mode,
                e.Range,
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
            UpdatePermissionsUi();        // 套用 登入/角色/測試中 權限與鎖定
            UpdateRelayPanelState();      // 測試中停用 Relay 小視窗手動控制
        }

        /// <summary>
        /// V2.2 依「登入狀態 + 角色 + 是否測試中」統一套用權限與鎖定（含 Operator 顯示）：
        /// - Settings(設備設定)頁：僅 Admin 且非測試中可編輯；未登入 / Operator / 測試中 一律反灰。
        /// - Debug Log「清除 Log」：測試中停用。
        /// - 登入：未登入且非測試中可用；登出：已登入且非測試中可用。
        /// 測試 / 暫停期間視為測試中（_running=true）→ Settings、清除 Log、登入、登出 皆鎖定。
        /// </summary>
        private void UpdatePermissionsUi()
        {
            bool testing = _running;
            bool loggedIn = _auth != null && _auth.IsLoggedIn;
            bool admin = _auth != null && _auth.IsAdmin;

            // V2.2：取消 Settings 權限與測試鎖定 — 任何人（含未登入 / 測試中）皆可查看與修改 Settings。
            // 測試中修改僅影響下一次測試（流程已對設定快照）。連線等操作不在此鎖定。
            tabDevice.Enabled = true;

            // 清除 Log：僅 Admin 可用（未登入 / Operator 停用）；測試中停用
            if (btnClearLog != null) btnClearLog.Enabled = admin && !testing;

            // 開啟 Log 資料夾：僅 Admin 顯示（未登入 / Operator 隱藏）；測試中停用
            if (btnOpenLogDir != null)
            {
                btnOpenLogDir.Visible = admin;
                btnOpenLogDir.Enabled = admin && !testing;
            }

            // 登出：登入後才顯示；測試中停用。（無登入按鈕，登入改自動觸發）
            if (btnLogout != null)
            {
                btnLogout.Visible = loggedIn;
                btnLogout.Enabled = loggedIn && !testing;
            }

            // 帳號管理：僅 Admin 顯示（未登入 / Operator 隱藏）；測試中停用
            if (btnAccountMgr != null)
            {
                btnAccountMgr.Visible = admin;
                btnAccountMgr.Enabled = admin && !testing;
            }

            if (lblOperator != null)
            {
                // 未登入：OP：未登入 / Operator：OP：工號 / Admin：Admin：工號
                if (!loggedIn)
                    lblOperator.Text = "OP：未登入";
                else if (admin)
                    lblOperator.Text = "Admin：" + _auth.OperatorId;
                else
                    lblOperator.Text = "OP：" + _auth.OperatorId;
                lblOperator.ForeColor = loggedIn ? OkGreen : Color.Red;
            }
        }

        /// <summary>未登入時切換到 Settings / Debug Log 先跳權限驗證；取消則不切換。</summary>
        private void TabMain_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (_auth != null && _auth.IsLoggedIn) return;
            if (e.TabPage == tabDevice || e.TabPage == tabLog)
            {
                if (!PromptLogin())
                    e.Cancel = true;   // 未登入 / 取消 → 留在原頁
            }
        }

        /// <summary>顯示登入視窗；成功則寫 Debug Log 並更新權限。回傳是否已登入。</summary>
        private bool PromptLogin()
        {
            using (var f = new LoginForm(_auth))
            {
                f.ShowDialog(this);
            }
            if (_auth.IsLoggedIn)
            {
                string roleName = _auth.IsAdmin ? "Admin" : "Operator";
                if (_debugLog != null)
                    _debugLog.Write(LogKind.Info, roleName + " Login Success : " + _auth.OperatorId);  // 不含密碼
                UpdatePermissionsUi();
                // 登入成功 Toast（不跳 MessageBox、不阻擋操作）
                Toast.Show(this, roleName + " 登入成功：" + _auth.OperatorId);

                // Admin 仍使用預設密碼 → 稍後提示修改（接在登入成功 Toast 之後）
                if (_auth.IsAdmin && _auth.UsingDefaultPassword)
                {
                    var hintTimer = new System.Windows.Forms.Timer { Interval = 2600 };
                    hintTimer.Tick += (s, e) =>
                    {
                        hintTimer.Stop();
                        hintTimer.Dispose();
                        Toast.Show(this, "建議立即修改預設管理員密碼。", 3000, 900);
                    };
                    hintTimer.Start();
                }
            }
            return _auth.IsLoggedIn;
        }

        private void BtnLogout_Click(object sender, EventArgs e)
        {
            if (_running || !_auth.IsLoggedIn) return;
            string who = _auth.OperatorId;
            _auth.Logout();   // 清空 CurrentOperatorId / CurrentRole
            if (_debugLog != null)
                _debugLog.Write(LogKind.Info, "Operator Logout : " + who);

            // 登出清空：測試紀錄(DataGridView) / 條碼 / 目前測試結果（保留 Debug Log、連線、Settings）
            dgvResults.Rows.Clear();
            txtBarcode.Clear();
            _testedBarcodes.Clear();
            SetResult("待測", Color.DimGray, Color.Gainsboro);
            ResetLiveStatus();
            UpdateSummaryStats(null);

            UpdatePermissionsUi();
            Toast.Show(this, "登出成功");
        }

        /// <summary>開啟 Admin 帳號管理對話框（僅 Admin；測試中此入口隨 Settings 頁停用）。</summary>
        private void OpenAccountManager()
        {
            if (_running) return;
            if (_auth == null || !_auth.IsAdmin)
            {
                MsgBox.Show(this, "權限不足", "僅 Admin 可開啟帳號管理。", MessageBoxIcon.Warning, "確定");
                return;
            }

            using (var f = new AccountManagerForm(_auth,
                msg => { if (_debugLog != null) _debugLog.Write(LogKind.Info, msg); }))
            {
                f.ShowDialog(this);
            }
            UpdatePermissionsUi();
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
