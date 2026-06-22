using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DX01_ShortCircuitTester.Services;

namespace DX01_ShortCircuitTester
{
    /// <summary>
    /// MainForm 的實機驗證 UI：Debug Log 分頁 + 設備測試 / 電表測試按鈕。
    /// 以程式碼建立並掛到既有的 tabMain / tabDevice，不更動 Designer。
    /// </summary>
    public partial class MainForm
    {
        private DebugLog _debugLog;

        private TabPage tabLog;
        private RichTextBox rtbLog;
        private Button btnClearLog;
        private Button btnOpenLogDir;

        // V2.2：Debug Log 檔案寫入（Logs 資料夾，10MB 切檔，90 天清理）
        private LogFileWriter _logFile;

        // 畫面只保留最近 N 行（避免 UI 變慢）
        private const int ScreenLogMaxLines = 1000;

        // Debug Log 批次更新：背景流程只入佇列，UI Timer 定時批次寫入，避免跨執行緒 / 大量 AppendText 卡頓
        private readonly Queue<LogEventArgs> _logQueue = new Queue<LogEventArgs>();
        private readonly object _logLock = new object();
        private System.Windows.Forms.Timer _logFlushTimer;


        private GroupBox gbDevTest;
        private Button btnRelayCycle;
        private Button btnGdmTest;
        private Label lblDevTestResult;

        private GroupBox gbDevInfo;
        private Label lblDevInfoGdm;
        private Label lblDevInfoRelay;

        private Button btnSettings;
        private Button btnAccountMgr;

        /// <summary>建立 Debug Log 分頁與設備測試區，並把日誌接到設備控制器。</summary>
        private void BuildDebugUi()
        {
            // 檔案 Log：建立寫入器並清除超過 90 天的舊檔（畫面清除不影響檔案）
            _logFile = new LogFileWriter();
            _logFile.CleanupOldLogs(90);

            _debugLog = new DebugLog();
            _debugLog.Entry += OnLogEntry;
            if (_meter != null) _meter.Log = _debugLog;
            if (_relay != null) _relay.Log = _debugLog;
            if (_flow != null) _flow.Log = _debugLog;

            // ===== Debug Log 分頁 =====
            tabLog = new TabPage("Debug Log");

            var logBar = new Panel { Dock = DockStyle.Top, Height = 38 };
            btnClearLog = new Button { Text = "清除 Log", Location = new Point(8, 5), Size = new Size(100, 28) };
            btnClearLog.Click += (s, e) =>
            {
                lock (_logLock) { _logQueue.Clear(); }
                try { if (rtbLog != null && !rtbLog.IsDisposed) rtbLog.Clear(); }
                catch { }
            };
            logBar.Controls.Add(btnClearLog);

            btnOpenLogDir = new Button { Text = "開啟 Log 資料夾", Location = new Point(116, 5), Size = new Size(140, 28) };
            btnOpenLogDir.Click += (s, e) => OpenLogFolder();
            logBar.Controls.Add(btnOpenLogDir);

            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 9.5F),
                DetectUrls = false,
                WordWrap = false
            };

            tabLog.Controls.Add(rtbLog);
            tabLog.Controls.Add(logBar);
            tabMain.TabPages.Add(tabLog);

            // 切換頁籤時只「顯示目前 Log」並捲到底，不重建控制項、不清空
            tabMain.SelectedIndexChanged += TabMain_SelectedIndexChanged;

            // UI Timer：每 300ms 把佇列中的 Log 批次寫入畫面
            _logFlushTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _logFlushTimer.Tick += (s, e) => FlushLogQueue();
            _logFlushTimer.Start();

            // ===== 設備測試區（加到「設備設定」頁） =====
            gbDevTest = new GroupBox
            {
                Text = "設備測試 (實機驗證)",
                Font = new Font("Microsoft JhengHei UI", 11F),
                Location = new Point(16, 400),
                Size = new Size(944, 120),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            btnRelayCycle = new Button
            {
                Text = "設備測試\n(Relay 00→01→10→11)",
                Location = new Point(24, 34),
                Size = new Size(240, 64),
                FlatStyle = FlatStyle.Flat
            };
            btnRelayCycle.Click += btnRelayCycle_Click;

            btnGdmTest = new Button
            {
                Text = "電表測試\n(*IDN? / READ?)",
                Location = new Point(280, 34),
                Size = new Size(240, 64),
                FlatStyle = FlatStyle.Flat
            };
            btnGdmTest.Click += btnGdmTest_Click;

            lblDevTestResult = new Label
            {
                Location = new Point(540, 28),
                Size = new Size(384, 80),
                Font = new Font("Consolas", 10F),
                ForeColor = Color.DimGray,
                Text = "(尚未測試)"
            };

            gbDevTest.Controls.Add(btnRelayCycle);
            gbDevTest.Controls.Add(btnGdmTest);
            gbDevTest.Controls.Add(lblDevTestResult);
            tabDevice.Controls.Add(gbDevTest);

            // ===== 設備資訊區 =====
            gbDevInfo = new GroupBox
            {
                Text = "設備資訊",
                Font = new Font("Microsoft JhengHei UI", 11F),
                Location = new Point(16, 530),
                Size = new Size(944, 92),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            lblDevInfoGdm = new Label
            {
                Location = new Point(24, 34),
                AutoSize = true,
                Font = new Font("Consolas", 10F),
                Text = "GDM Identify: -"
            };
            lblDevInfoRelay = new Label
            {
                Location = new Point(24, 60),
                AutoSize = true,
                Font = new Font("Consolas", 10F),
                Text = "Relay VID/PID: 16C0:05DF  (未連線)"
            };
            gbDevInfo.Controls.Add(lblDevInfoGdm);
            gbDevInfo.Controls.Add(lblDevInfoRelay);
            tabDevice.Controls.Add(gbDevInfo);

            // ===== 參數設定按鈕 =====
            btnSettings = new Button
            {
                Text = "參數設定…",
                Location = new Point(16, 632),
                Size = new Size(200, 40),
                Font = new Font("Microsoft JhengHei UI", 11F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            btnSettings.Click += (s, e) => OpenSettingForm();
            tabDevice.Controls.Add(btnSettings);

            // V2.2：帳號管理（Admin 限定；位於 Settings 頁，測試中隨整頁停用）
            btnAccountMgr = new Button
            {
                Text = "帳號管理",
                Location = new Point(232, 632),
                Size = new Size(160, 40),
                Font = new Font("Microsoft JhengHei UI", 11F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            btnAccountMgr.Click += (s, e) => OpenAccountManager();
            tabDevice.Controls.Add(btnAccountMgr);
        }

        /// <summary>
        /// 日誌事件處理：可能來自任何執行緒。只做過濾 + 入佇列，不直接碰 UI 控制項，
        /// 由 UI Timer（FlushLogQueue）批次更新，徹底避免跨執行緒存取。
        /// </summary>
        private void OnLogEntry(object sender, LogEventArgs e)
        {
            try
            {
                if (e == null)
                    return;

                // 檔案：完整寫入（不受畫面 DebugLevel 過濾，確保可完整追溯）。
                if (_logFile != null)
                    _logFile.Append(string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}", e.Time, e.Kind, e.Message));

                // 畫面：Status（等待 Power 狀態，已於流程節流）一律顯示；其餘依 DebugLevel 過濾後入佇列。
                if (e.Kind != LogKind.Status && !AllowLog(e.Kind, AppSettings.Current.DebugLevel))
                    return;

                lock (_logLock)
                {
                    _logQueue.Enqueue(e);
                    // 佇列本身也設上限，避免背景大量寫入造成記憶體成長
                    int cap = Math.Max(500, AppSettings.Current.MaxDebugLogLines * 2);
                    while (_logQueue.Count > cap)
                        _logQueue.Dequeue();
                }
            }
            catch
            {
                // Debug Log 寫入失敗不可中斷測試流程
            }
        }

        /// <summary>UI 執行緒批次將佇列內容寫入 RichTextBox（含 IsDisposed/IsHandleCreated 與例外保護）。</summary>
        private void FlushLogQueue()
        {
            if (rtbLog == null || rtbLog.IsDisposed || !rtbLog.IsHandleCreated)
                return;

            LogEventArgs[] batch;
            lock (_logLock)
            {
                if (_logQueue.Count == 0)
                    return;
                batch = _logQueue.ToArray();
                _logQueue.Clear();
            }

            try
            {
                foreach (var e in batch)
                    AppendLogLine(e);

                TrimLog();

                rtbLog.SelectionStart = rtbLog.TextLength;
                rtbLog.ScrollToCaret();
            }
            catch
            {
                // 忽略 UI log 例外，不影響測試流程
            }
        }

        /// <summary>將單筆日誌依類別上色後 append（僅在 UI 執行緒呼叫）。</summary>
        private void AppendLogLine(LogEventArgs e)
        {
            Color color;
            string prefix;
            switch (e.Kind)
            {
                case LogKind.Relay: color = Color.MediumBlue; prefix = ""; break;       // 訊息已是 "Relay 00"
                case LogKind.Tx: color = Color.DarkGreen; prefix = "TX: "; break;
                case LogKind.Rx: color = Color.FromArgb(0, 128, 0); prefix = "RX: "; break;
                case LogKind.Error: color = Color.Firebrick; prefix = "ERR: "; break;
                case LogKind.Status: color = Color.SteelBlue; prefix = ""; break;       // 等待 Power 狀態（已於流程節流）
                default: color = Color.DimGray; prefix = ""; break;
            }

            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionColor = color;
            rtbLog.AppendText(string.Format("[{0:HH:mm:ss.fff}] {1}{2}{3}",
                e.Time, prefix, e.Message, Environment.NewLine));
        }

        /// <summary>畫面只保留最近 ScreenLogMaxLines(1000) 行，超過則移除最舊的行（避免 UI 變慢）。</summary>
        private void TrimLog()
        {
            int max = ScreenLogMaxLines;
            if (max <= 0)
                return;

            int lineCount = rtbLog.Lines.Length;
            if (lineCount <= max)
                return;

            int removeLines = lineCount - max;
            int cut = rtbLog.GetFirstCharIndexFromLine(removeLines);
            if (cut > 0)
            {
                rtbLog.Select(0, cut);
                rtbLog.SelectedText = "";
            }
        }

        /// <summary>開啟 Logs 資料夾（不存在則建立）。</summary>
        private void OpenLogFolder()
        {
            if (_auth == null || !_auth.IsAdmin)   // 防護：僅 Admin
                return;
            try
            {
                string dir = LogFileWriter.LogDirectory;
                System.IO.Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            catch (Exception ex)
            {
                MsgBox.Show(this, "開啟失敗", "無法開啟 Log 資料夾：\n" + ex.Message, MessageBoxIcon.Warning, "確定");
            }
        }

        /// <summary>切換到 Debug Log 頁籤時：只顯示目前 Log 並捲到底（不重建 / 不清空）。</summary>
        private void TabMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabMain.SelectedTab != tabLog)
                return;
            if (rtbLog == null || rtbLog.IsDisposed || !rtbLog.IsHandleCreated)
                return;

            try
            {
                FlushLogQueue(); // 先把待寫入的批次顯示出來
                rtbLog.SelectionStart = rtbLog.TextLength;
                rtbLog.ScrollToCaret();
            }
            catch
            {
                // 顯示錯誤不可中斷流程
            }
        }

        /// <summary>依 DebugLevel 過濾：error=只錯誤；info=錯誤/Relay/Info；debug=全部。</summary>
        private static bool AllowLog(LogKind kind, string level)
        {
            if (string.Equals(level, "debug", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(level, "info", StringComparison.OrdinalIgnoreCase))
                return kind != LogKind.Tx && kind != LogKind.Rx;
            return kind == LogKind.Error; // error 或未知
        }

        // ===== 設備測試：Relay 00→01→10→11 逐步切換 =====
        private async void btnRelayCycle_Click(object sender, EventArgs e)
        {
            if (!_relay.IsConnected)
            {
                MsgBox.Show(this, "Relay 未連線", "USB Relay 尚未連線，請先連線 Relay。", MessageBoxIcon.Warning, "確定");
                return;
            }

            string[] codes = { "00", "01", "10", "11" };
            btnRelayCycle.Enabled = false;
            btnGdmTest.Enabled = false;
            try
            {
                _debugLog.Write(LogKind.Info, "=== 設備測試 (Relay 循環) 開始 ===");
                foreach (string code in codes)
                {
                    _relay.SetRelay(code); // RelayChanged 事件會同步更新 lblRelay
                    lblDevTestResult.Text = "Relay 目前狀態: " + code;
                    await Task.Delay(700);
                }
                _relay.SetRelay("00"); // 復位 00，UI 同步顯示 00
                lblDevTestResult.Text = "Relay 循環完成，已復位 00";
                _debugLog.Write(LogKind.Info, "=== 設備測試完成 ===");
            }
            catch (Exception ex)
            {
                _debugLog.Write(LogKind.Error, "Relay 測試失敗: " + ex.Message);
                MsgBox.Show(this, "錯誤", "Relay 測試失敗:\n" + ex.Message, MessageBoxIcon.Error, "確定");
                UpdateConnStatus();
            }
            finally
            {
                btnRelayCycle.Enabled = true;
                btnGdmTest.Enabled = true;
            }
        }

        // ===== 電表測試：*IDN? + READ? =====
        private async void btnGdmTest_Click(object sender, EventArgs e)
        {
            if (!_meter.IsConnected)
            {
                MsgBox.Show(this, "電表未連線", "電表尚未連線，請先連線 GDM-8261A。", MessageBoxIcon.Warning, "確定");
                return;
            }

            btnRelayCycle.Enabled = false;
            btnGdmTest.Enabled = false;
            try
            {
                _debugLog.Write(LogKind.Info, "=== 電表測試 (*IDN? / READ?) 開始 ===");
                string idn = _meter.Identify();
                lblGdmIdn.Text = idn;
                await Task.Delay(150);
                double value = _meter.Read();

                lblDevTestResult.Text = "*IDN? = " + idn + Environment.NewLine +
                                        "READ? = " + value.ToString("0.######");
                _debugLog.Write(LogKind.Info, "=== 電表測試完成 ===");
            }
            catch (Exception ex)
            {
                _debugLog.Write(LogKind.Error, "電表測試失敗: " + ex.Message);
                UpdateConnStatus();
                // 通訊失敗時連線已被釋放：LAN 斷線顯示專用提示，否則顯示一般錯誤
                if (_meter.UseLan && !_meter.IsConnected)
                    NotifyLanDisconnected();
                else
                    MsgBox.Show(this, "錯誤", "電表測試失敗:\n" + ex.Message, MessageBoxIcon.Error, "確定");
            }
            finally
            {
                btnRelayCycle.Enabled = true;
                btnGdmTest.Enabled = true;
            }
        }
    }
}
