using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DX01_ShortCircuitTester.Device;   // 共用：來自 DX01_Common（namespace 不變）
using DX01_ShortCircuitTester.Services;

namespace DX01_StressTester
{
    /// <summary>
    /// DX01 壓力測試程式（工程驗證）。
    /// 通訊 / 設定 / Log 重用 DX01_Common；Step 序列與判定由 StressConfig 驅動（與 OP 互不影響）。
    /// Phase 1：設備狀態、Loop Count、Start/Stop、PASS/FAIL 統計、即時 Log、
    /// Step1~Step12 設定（DataGridView + JSON 儲存/讀取）、TC-P3-001 循環測試。
    /// </summary>
    public partial class StressForm : Form
    {
        // 共用設備（皆來自 DX01_Common）
        private readonly RealRelayController _relay = new RealRelayController();
        private readonly RealGdm8261AController _meter = new RealGdm8261AController();
        private readonly DebugLog _log = new DebugLog();
        private readonly StressTestRunner _runner;

        private StressConfig _config;

        // 執行狀態
        private CancellationTokenSource _cts;
        private bool _running;
        private int _passCount;
        private int _failCount;
        private readonly Stopwatch _sw = new Stopwatch();

        private static readonly Color OkGreen = Color.FromArgb(46, 160, 67);
        private static readonly Color NgRed = Color.FromArgb(211, 47, 47);

        public StressForm()
        {
            InitializeComponent();

            // Log 匯流排：設備寫入 → UI 即時顯示
            _meter.Log = _log;
            _relay.Log = _log;
            _log.Entry += OnLogEntry;

            _runner = new StressTestRunner(_relay, _meter) { Log = (tag, msg) => Log(tag, msg) };
        }

        // ------------------------------------------------------------ 生命週期

        private void StressForm_Load(object sender, EventArgs e)
        {
            AppSettings.Load();   // 共用 Config（連線參數）

            // Step 設定：讀取 Config\StressTestConfig.json（不存在則用預設並寫檔）
            _config = StressConfig.Load();
            if (!System.IO.File.Exists(StressConfig.ConfigPath))
            {
                try { _config.Save(); } catch { }
            }
            PopulateGrid(_config);

            if (cbLoopCount.Items.Count > 0)
                cbLoopCount.SelectedIndex = 0;

            if (cbNgAction.Items.Count > 0)
                cbNgAction.SelectedIndex = 0;   // 預設 Stop Test（觸發顯示更新）
            UpdateNgActionLabel();

            uiTimer.Start();
            RefreshDeviceStatus();
            SetRunningUi(false);
            Log("SYS", "DX01 Stress Tester 已啟動");
        }

        private void StressForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try { _cts?.Cancel(); } catch { }
            try { _meter.Disconnect(); } catch { }
            try { _relay.Disconnect(); } catch { }
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (!_running)
                RefreshDeviceStatus();
            if (_sw.IsRunning)
                UpdateElapsed();
        }

        // ---------------------------------------------------------------- 設備

        private void ApplyConnSettings()
        {
            // 連線參數一律取自共用 AppSettings（與 OP 同一份 Config）
            var c = AppSettings.Current;
            _meter.UseLan = c.ConnectionMode == GdmConnectionMode.Lan;
            _meter.Ip = c.Ip;
            _meter.TcpPort = c.TcpPort;
            _meter.BaudRate = c.ComBaud;
            // 序列埠模式需 COM 名稱（AppSettings 未保存）；Phase 1 以 LAN 為主。
        }

        private bool EnsureConnected()
        {
            try
            {
                ApplyConnSettings();
                if (!_meter.IsConnected) _meter.Connect();
                if (!_relay.IsConnected) _relay.Connect();
            }
            catch (Exception ex)
            {
                Log("ERR", "連線失敗：" + ex.Message);
                RefreshDeviceStatus();
                MessageBox.Show(this,
                    "設備連線失敗：\n" + ex.Message + "\n\n請確認 GDM LAN / USB Relay 是否就緒。",
                    "連線失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            RefreshDeviceStatus();
            return _meter.IsConnected && _relay.IsConnected;
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (_running) return;
            if (EnsureConnected())
                Log("SYS", "設備連線完成");
        }

        private void BtnDisconnect_Click(object sender, EventArgs e)
        {
            if (_running) return;
            try { _meter.Disconnect(); } catch { }
            try { _relay.Disconnect(); } catch { }
            RefreshDeviceStatus();
            Log("SYS", "設備已中斷");
        }

        private void RefreshDeviceStatus()
        {
            bool g = _meter.IsConnected;
            bool r = _relay.IsConnected;
            lblGdm.Text = "GDM：" + (g ? "Connected" : "Disconnected");
            lblGdm.ForeColor = g ? OkGreen : NgRed;
            lblRelay.Text = "Relay：" + (r ? "Connected" : "Disconnected");
            lblRelay.ForeColor = r ? OkGreen : NgRed;
        }

        // -------------------------------------------------- TC-P3-001 循環測試

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            if (_running) return;

            int loopCount;
            if (!int.TryParse((cbLoopCount.Text ?? "").Trim(), out loopCount) || loopCount <= 0)
            {
                MessageBox.Show(this, "請輸入正確的 Loop Count（正整數）。", "設定錯誤",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 以目前 Grid 內容（含未存檔的編輯）作為本次執行的 Step 設定
            StressConfig cfg;
            if (!TryReadGrid(out cfg))
                return;
            _config = cfg;

            if (!EnsureConnected())
                return;

            NgAction ngAction = SelectedNgAction();
            UpdateNgActionLabel();

            _passCount = 0;
            _failCount = 0;
            UpdateStats(0, loopCount);
            _cts = new CancellationTokenSource();
            _sw.Restart();
            SetRunningUi(true);
            Log("SYS", "開始循環測試，Loop Count = " + loopCount + "，NG Action = " + cbNgAction.Text);

            try
            {
                for (int loop = 1; loop <= loopCount; loop++)
                {
                    if (_cts.IsCancellationRequested) break;
                    UpdateStats(loop, loopCount);

                    LoopOutcome outcome;
                    try
                    {
                        outcome = await _runner.RunLoopAsync(loop, _config, ngAction, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Log("SYS", "Loop " + loop + " 已中止");
                        break;
                    }
                    catch (Exception ex)
                    {
                        // 最後防線：不跳到 Visual Studio 例外畫面
                        _failCount++;
                        Log("ERR", "Loop " + loop + " 例外：" + ex.Message);
                        UpdateStats(loop, loopCount);
                        break;
                    }

                    if (outcome == LoopOutcome.Pass) _passCount++;
                    else _failCount++;
                    UpdateStats(loop, loopCount);
                    Log(outcome == LoopOutcome.Pass ? "PASS" : "FAIL",
                        "Loop " + loop + " → " + outcome + "  (PASS=" + _passCount + " FAIL=" + _failCount + ")");

                    if (outcome == LoopOutcome.Stopped)
                    {
                        Log("SYS", "StopOnFail 觸發，停止循環測試");
                        break;
                    }
                    if (outcome == LoopOutcome.DeviceError)
                    {
                        Log("SYS", "設備異常，停止循環測試");
                        break;
                    }
                }
            }
            finally
            {
                _sw.Stop();
                UpdateElapsed();
                SetRunningUi(false);
                RefreshDeviceStatus();
                Log("SYS", "循環測試結束。PASS=" + _passCount + " FAIL=" + _failCount);
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (!_running) return;
            Log("SYS", "使用者要求停止…");
            try { _cts?.Cancel(); } catch { }
        }

        // -------------------------------------------------------- NG Action

        private void CbNgAction_Changed(object sender, EventArgs e)
        {
            UpdateNgActionLabel();
        }

        private void UpdateNgActionLabel()
        {
            lblNgAction.Text = "NG Action：" + (cbNgAction.SelectedItem ?? "Stop Test");
        }

        /// <summary>目前選擇的全域 NG 行為（Step StopOnFail=false 時套用）。</summary>
        private NgAction SelectedNgAction()
        {
            switch (cbNgAction.SelectedIndex)
            {
                case 1: return NgAction.ContinueLoop;
                case 2: return NgAction.ContinueNextStep;
                default: return NgAction.StopTest;
            }
        }

        // ------------------------------------------------------- Step 設定 / JSON

        private void BtnReloadCfg_Click(object sender, EventArgs e)
        {
            if (_running) return;
            _config = StressConfig.Load();
            PopulateGrid(_config);
            Log("SYS", "已重新載入 Step 設定");
        }

        private void BtnSaveCfg_Click(object sender, EventArgs e)
        {
            if (_running) return;
            StressConfig cfg;
            if (!TryReadGrid(out cfg))
                return;
            try
            {
                cfg.Save();
                _config = cfg;
                Log("SYS", "Step 設定已儲存至 " + StressConfig.ConfigPath);
                MessageBox.Show(this, "Step 設定已儲存。", "儲存成功",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("ERR", "儲存失敗：" + ex.Message);
                MessageBox.Show(this, "儲存失敗：\n" + ex.Message, "錯誤",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDefaultCfg_Click(object sender, EventArgs e)
        {
            if (_running) return;
            if (MessageBox.Show(this, "確定要還原為預設 Step 設定？（未儲存的修改會遺失）",
                    "還原預設", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            _config = StressConfig.Default();
            PopulateGrid(_config);
            Log("SYS", "已還原預設 Step 設定（尚未儲存）");
        }

        private void DgvSteps_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false; // 避免 combobox/型別轉換錯誤跳例外
        }

        // -------------------------------------------------------------- Grid

        private void PopulateGrid(StressConfig cfg)
        {
            dgvSteps.Rows.Clear();
            foreach (StressStep s in cfg.Steps)
            {
                int i = dgvSteps.Rows.Add();
                DataGridViewRow row = dgvSteps.Rows[i];
                row.Cells[colStepNo.Index].Value = s.StepNo;
                row.Cells[colStepName.Index].Value = s.StepName;
                row.Cells[colEnable.Index].Value = s.Enable;
                row.Cells[colRelayChannel.Index].Value = NormChannel(s.RelayChannel);
                row.Cells[colRelayOn.Index].Value = s.RelayOn;
                row.Cells[colMeasureMode.Index].Value = NormMode(s.MeasureMode);
                row.Cells[colMin.Index].Value = s.MinValue.HasValue ? s.MinValue.Value.ToString(CultureInfo.InvariantCulture) : "";
                row.Cells[colMax.Index].Value = s.MaxValue.HasValue ? s.MaxValue.Value.ToString(CultureInfo.InvariantCulture) : "";
                row.Cells[colUnit.Index].Value = NormUnit(s.Unit);
                row.Cells[colWaitMs.Index].Value = s.WaitMs;
                row.Cells[colRetry.Index].Value = s.RetryCount;
                row.Cells[colStopOnFail.Index].Value = s.StopOnFail;
                row.Cells[colRemark.Index].Value = s.Remark;
            }
        }

        private bool TryReadGrid(out StressConfig cfg)
        {
            dgvSteps.EndEdit();
            cfg = new StressConfig();

            foreach (DataGridViewRow row in dgvSteps.Rows)
            {
                if (row.IsNewRow) continue;

                int stepNo = ToInt(row.Cells[colStepNo.Index].Value, 0);

                double? min, max;
                if (!TryParseLimit(row.Cells[colMin.Index].Value, out min))
                {
                    LimitError(stepNo, "Min");
                    return false;
                }
                if (!TryParseLimit(row.Cells[colMax.Index].Value, out max))
                {
                    LimitError(stepNo, "Max");
                    return false;
                }

                cfg.Steps.Add(new StressStep
                {
                    StepNo = stepNo,
                    StepName = ToStr(row.Cells[colStepName.Index].Value),
                    Enable = ToBool(row.Cells[colEnable.Index].Value),
                    RelayChannel = NormChannel(ToStr(row.Cells[colRelayChannel.Index].Value)),
                    RelayOn = ToBool(row.Cells[colRelayOn.Index].Value),
                    MeasureMode = NormMode(ToStr(row.Cells[colMeasureMode.Index].Value)),
                    MinValue = min,
                    MaxValue = max,
                    Unit = NormUnit(ToStr(row.Cells[colUnit.Index].Value)),
                    WaitMs = ToInt(row.Cells[colWaitMs.Index].Value, 0),
                    RetryCount = ToInt(row.Cells[colRetry.Index].Value, 0),
                    StopOnFail = ToBool(row.Cells[colStopOnFail.Index].Value),
                    Remark = ToStr(row.Cells[colRemark.Index].Value)
                });
            }
            return true;
        }

        private void LimitError(int stepNo, string field)
        {
            MessageBox.Show(this, "Step" + stepNo + " 的 " + field + " 數值格式錯誤（請輸入數字，或留空表示不檢查）。",
                "設定錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // --------------------------------------------------------- 共用 helper

        private static bool TryParseLimit(object cell, out double? value)
        {
            value = null;
            string s = (cell == null ? "" : cell.ToString()).Trim();
            if (s.Length == 0) return true; // 空 = 不檢查
            double d;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out d) ||
                double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out d))
            {
                value = d;
                return true;
            }
            return false;
        }

        private static int ToInt(object cell, int def)
        {
            int v;
            string s = (cell == null ? "" : cell.ToString()).Trim();
            return int.TryParse(s, out v) ? v : def;
        }

        private static bool ToBool(object cell)
        {
            return cell is bool && (bool)cell;
        }

        private static string ToStr(object cell)
        {
            return cell == null ? "" : cell.ToString();
        }

        private static string NormChannel(string s)
        {
            if (s == "1" || s == "2") return s;
            if (string.Equals(s, "Both", StringComparison.OrdinalIgnoreCase)) return "Both";
            return "None";
        }

        private static string NormMode(string s)
        {
            if (string.Equals(s, "Current", StringComparison.OrdinalIgnoreCase)) return "Current";
            if (string.Equals(s, "Resistance", StringComparison.OrdinalIgnoreCase)) return "Resistance";
            return "Voltage";
        }

        private static string NormUnit(string s)
        {
            if (s == "mA" || s == "A" || s == "Ω") return s;
            return "V";
        }

        // -------------------------------------------------------------- 狀態 / Log

        private void SetRunningUi(bool running)
        {
            _running = running;
            btnStart.Enabled = !running;
            btnStop.Enabled = running;
            btnConnect.Enabled = !running;
            btnDisconnect.Enabled = !running;
            cbLoopCount.Enabled = !running;
            cbNgAction.Enabled = !running;
            dgvSteps.Enabled = !running;
            btnReloadCfg.Enabled = !running;
            btnSaveCfg.Enabled = !running;
            btnDefaultCfg.Enabled = !running;
        }

        private void UpdateStats(int currentLoop, int total)
        {
            lblCurrentLoop.Text = "Current Loop：" + currentLoop + " / " + total;
            lblPass.Text = "PASS：" + _passCount;
            lblFail.Text = "FAIL：" + _failCount;
            UpdateElapsed();
        }

        private void UpdateElapsed()
        {
            TimeSpan t = _sw.Elapsed;
            lblElapsed.Text = string.Format("Elapsed：{0:00}:{1:00}:{2:00}",
                (int)t.TotalHours, t.Minutes, t.Seconds);
        }

        private void OnLogEntry(object sender, LogEventArgs e)
        {
            Log(e.Kind.ToString().ToUpperInvariant(), e.Message, e.Time);
        }

        private void Log(string tag, string message)
        {
            Log(tag, message, DateTime.Now);
        }

        private void Log(string tag, string message, DateTime time)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke((Action)(() => Log(tag, message, time))); }
                catch { }
                return;
            }

            // 行數上限：超過移除最舊（避免長時間測試記憶體膨脹）
            int max = AppSettings.Current.MaxDebugLogLines;
            if (max > 0 && txtLog.Lines.Length > max)
            {
                var lines = txtLog.Lines;
                var keep = new string[max / 2];
                Array.Copy(lines, lines.Length - keep.Length, keep, 0, keep.Length);
                txtLog.Lines = keep;
            }

            txtLog.AppendText("[" + time.ToString("HH:mm:ss") + "] " + tag + "  " + message + Environment.NewLine);
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }
    }
}
