using System;
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

        private int _totalCount;
        private int _passCount;
        private int _ngCount;

        private static readonly Color OkGreen = Color.FromArgb(46, 160, 67);
        private static readonly Color NgRed = Color.FromArgb(211, 47, 47);

        private System.Windows.Forms.Timer _okMsgTimer;

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
            UpdateConnStatus();
            UpdateInfo();

            tabMain.SelectedTab = tabTest;
            txtBarcode.Focus();
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
            try { _relay.Connect(); } catch { /* 未接 Relay，顯示未連線即可 */ }

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

            try
            {
                ApplyGdmConnectionSettings();
                _meter.Connect();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "電表連線失敗:\n" + ex.Message, "錯誤",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateConnStatus();
        }

        private void btnGdmDisconnect_Click(object sender, EventArgs e)
        {
            _meter.Disconnect();
            UpdateConnStatus();
        }

        private void btnRelayRefresh_Click(object sender, EventArgs e)
        {
            if (_relay.IsConnected)
                return;

            lblRelayStatus.ForeColor = Color.Firebrick;
            lblRelayStatus.Text = _relay.DetectDevice() ? "● 已偵測，未連線" : "● 未連線 (未偵測)";
        }

        private void btnRelayConnect_Click(object sender, EventArgs e)
        {
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
                lblGdmStatus.Text = _meter.UseLan
                    ? "● 已連線  GDM8261A  LAN " + _meter.Ip + ":" + _meter.TcpPort
                    : "● 已連線  GDM8261A  " + _meter.PortName + "  " + _meter.BaudRate;
                lblGdmStatus.ForeColor = OkGreen;
            }
            else
            {
                lblGdmStatus.Text = "● 未連線";
                lblGdmStatus.ForeColor = Color.Firebrick;
            }
            lblGdmIdn.Text = (g && !string.IsNullOrEmpty(_meter.Idn)) ? _meter.Idn : "";

            if (r)
            {
                lblRelayStatus.Text = "● USB Relay Connected";
                lblRelayStatus.ForeColor = OkGreen;
            }
            else
            {
                lblRelayStatus.Text = "● 未連線";
                lblRelayStatus.ForeColor = Color.Firebrick;
            }

            lblConn.Text = "電表: " + (g ? "連線" : "未連線") + "    Relay: " + (r ? "連線" : "未連線");
            lblConn.ForeColor = (g && r) ? OkGreen : Color.Firebrick;

            // 設備資訊區
            if (lblDevInfoGdm != null)
                lblDevInfoGdm.Text = "GDM Identify: " +
                    (g ? (string.IsNullOrEmpty(_meter.Idn) ? "(無回應)" : _meter.Idn) : "-");
            if (lblDevInfoRelay != null)
                lblDevInfoRelay.Text = "Relay VID/PID: 16C0:05DF  " + (r ? "(已連線)" : "(未連線)");

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
                    ShowBarcodeMsg("✕ 條碼格式錯誤", Color.Firebrick, false);
                    MessageBox.Show(this,
                        "條碼/序號不符合規則。\n\n規則: " + pattern + "\n輸入: " + (sn.Length == 0 ? "(空白)" : sn),
                        "條碼格式錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtBarcode.SelectAll();
                    txtBarcode.Focus();
                    return; // 不執行測試
                }
            }
            else if (sn.Length == 0)
            {
                ShowBarcodeMsg("✕ 請輸入序號", Color.Firebrick, false);
                return;
            }

            // 符合 → 顯示 OK（1 秒後消失）並自動進入測試流程
            ShowBarcodeMsg("✓ OK", OkGreen, true);
            StartTest();
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

        private async void StartTest()
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

            string sn = txtBarcode.Text.Trim();
            if (sn.Length == 0)
            {
                MessageBox.Show(this, "請先掃描或輸入條碼/序號。", "尚未輸入序號",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtBarcode.Focus();
                return;
            }

            // 重置畫面（Return to Step1）
            dgvResults.Rows.Clear();
            lblMeasure.Text = "---";
            lblRelay.Text = "--";
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
                SetResult("錯誤", Color.White, Color.DarkOrange);
                SetRunningState(false);
                // 設備可能因例外失聯，更新狀態
                UpdateConnStatus();
                return;
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
            }

            OnTestFinished(result);
        }

        private void OnTestFinished(TestResult result)
        {
            _totalCount++;

            if (result.Aborted)
            {
                SetResult("中止", Color.White, Color.DarkOrange);
            }
            else if (result.IsPass)
            {
                _passCount++;
                SetResult("OK", Color.White, OkGreen);
            }
            else
            {
                _ngCount++;
                SetResult("NG", Color.White, NgRed);
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
            UpdateInfo(result, logFile);

            // Return to Step1：清空序號、聚焦條碼，等待下一片
            txtBarcode.Clear();
            txtBarcode.Focus();
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
                e.StepNumber,
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

        private void UpdateInfo(TestResult result = null, string logFile = null)
        {
            string baseInfo = string.Format(
                "總數: {0}    OK: {1}    NG: {2}", _totalCount, _passCount, _ngCount);

            if (result != null)
            {
                string detail = "    最後: " + result.SerialNumber + " = " + result.Judgement;
                if (!result.IsPass && !result.Aborted && result.FirstFailedStep != null)
                    detail += " (Step " + result.FirstFailedStep.StepNumber + " " + result.FirstFailedStep.StepName + ")";
                baseInfo += detail;
            }

            if (!string.IsNullOrEmpty(logFile))
                baseInfo += "    紀錄: " + logFile;

            lblInfo.Text = baseInfo;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
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
