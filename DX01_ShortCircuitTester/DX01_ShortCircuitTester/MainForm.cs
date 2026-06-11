using System;
using System.Drawing;
using System.IO.Ports;
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

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // 鮑率選項
            cbGdmBaud.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });
            cbGdmBaud.SelectedItem = "115200";

            InitDevices();
            BuildDebugUi();
            RefreshGdmPorts();

            SetRunningState(false);
            SetResult("待測", Color.DimGray, Color.Gainsboro);

            txtBarcode.KeyDown += TxtBarcode_KeyDown;

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

            if (cbGdmPort.SelectedItem != null)
            {
                try
                {
                    _meter.PortName = cbGdmPort.SelectedItem.ToString();
                    _meter.BaudRate = GetSelectedBaud();
                    _meter.Connect();
                }
                catch { /* COM 不對或未接，維持未連線 */ }
            }
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
            if (cbGdmPort.SelectedItem == null)
            {
                MessageBox.Show(this, "請先選擇 COM Port（按重新整理偵測）。", "尚未選擇 COM Port",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _meter.PortName = cbGdmPort.SelectedItem.ToString();
                _meter.BaudRate = GetSelectedBaud();
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
                lblGdmStatus.Text = "● 已連線  GDM8261A  " + _meter.PortName + "  " + _meter.BaudRate;
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

            UpdateStartEnabled();
        }

        /// <summary>設備未連線時停用「開始測試」。</summary>
        private void UpdateStartEnabled()
        {
            btnStart.Enabled = !_running && _meter.IsConnected && _relay.IsConnected;
        }

        #endregion

        #region 測試頁

        private void TxtBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                if (!_running)
                    StartTest();
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            StartTest();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (_cts != null)
                _cts.Cancel();
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
            row.DefaultCellStyle.BackColor = e.Pass
                ? Color.FromArgb(232, 245, 233)
                : Color.FromArgb(255, 235, 238);
            row.DefaultCellStyle.ForeColor = e.Pass
                ? Color.FromArgb(27, 94, 32)
                : Color.FromArgb(183, 28, 28);

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
            btnStop.Enabled = running;
            txtBarcode.Enabled = !running;
            tabDevice.Enabled = !running; // 測試中不可改設備設定
            UpdateStartEnabled();         // 設備未連線或測試中 → 停用開始測試
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
