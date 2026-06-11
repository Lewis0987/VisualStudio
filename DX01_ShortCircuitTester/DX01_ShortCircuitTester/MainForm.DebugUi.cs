using System;
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

        private GroupBox gbDevTest;
        private Button btnRelayCycle;
        private Button btnGdmTest;
        private Label lblDevTestResult;

        private GroupBox gbDevInfo;
        private Label lblDevInfoGdm;
        private Label lblDevInfoRelay;

        /// <summary>建立 Debug Log 分頁與設備測試區，並把日誌接到設備控制器。</summary>
        private void BuildDebugUi()
        {
            _debugLog = new DebugLog();
            _debugLog.Entry += OnLogEntry;
            if (_meter != null) _meter.Log = _debugLog;
            if (_relay != null) _relay.Log = _debugLog;

            // ===== Debug Log 分頁 =====
            tabLog = new TabPage("Debug Log");

            var logBar = new Panel { Dock = DockStyle.Top, Height = 38 };
            btnClearLog = new Button { Text = "清除 Log", Location = new Point(8, 5), Size = new Size(100, 28) };
            btnClearLog.Click += (s, e) => rtbLog.Clear();
            logBar.Controls.Add(btnClearLog);

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

            // ===== 設備測試區（加到「設備設定」頁） =====
            gbDevTest = new GroupBox
            {
                Text = "設備測試 (實機驗證)",
                Font = new Font("Microsoft JhengHei UI", 11F),
                Location = new Point(16, 360),
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
                Location = new Point(16, 490),
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
        }

        private void OnLogEntry(object sender, LogEventArgs e)
        {
            if (rtbLog == null)
                return;

            if (rtbLog.InvokeRequired)
            {
                rtbLog.BeginInvoke(new Action(() => AppendLog(e)));
                return;
            }
            AppendLog(e);
        }

        private void AppendLog(LogEventArgs e)
        {
            Color color;
            string prefix;
            switch (e.Kind)
            {
                case LogKind.Relay: color = Color.MediumBlue; prefix = ""; break;       // 訊息已是 "Relay 00"
                case LogKind.Tx: color = Color.DarkGreen; prefix = "TX: "; break;
                case LogKind.Rx: color = Color.FromArgb(0, 128, 0); prefix = "RX: "; break;
                case LogKind.Error: color = Color.Firebrick; prefix = "ERR: "; break;
                default: color = Color.DimGray; prefix = ""; break;
            }

            // 防止 Log 無限成長
            if (rtbLog.TextLength > 120000)
            {
                rtbLog.Select(0, 40000);
                rtbLog.SelectedText = "";
            }

            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionColor = color;
            rtbLog.AppendText(string.Format("[{0:HH:mm:ss.fff}] {1}{2}{3}",
                e.Time, prefix, e.Message, Environment.NewLine));
            rtbLog.ScrollToCaret();
        }

        // ===== 設備測試：Relay 00→01→10→11 逐步切換 =====
        private async void btnRelayCycle_Click(object sender, EventArgs e)
        {
            if (!_relay.IsConnected)
            {
                MessageBox.Show(this, "Relay未連線", "Relay未連線",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    _relay.SetRelay(code);
                    lblDevTestResult.Text = "Relay 目前狀態: " + code;
                    lblRelay.Text = code;
                    lblRelay.ForeColor = Color.MediumBlue;
                    await Task.Delay(700);
                }
                _relay.SetRelay("00");
                lblDevTestResult.Text = "Relay 循環完成，已復位 00";
                _debugLog.Write(LogKind.Info, "=== 設備測試完成 ===");
            }
            catch (Exception ex)
            {
                _debugLog.Write(LogKind.Error, "Relay 測試失敗: " + ex.Message);
                MessageBox.Show(this, "Relay 測試失敗:\n" + ex.Message, "錯誤",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show(this, "電表未連線", "電表未連線",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(this, "電表測試失敗:\n" + ex.Message, "錯誤",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateConnStatus();
            }
            finally
            {
                btnRelayCycle.Enabled = true;
                btnGdmTest.Enabled = true;
            }
        }
    }
}
