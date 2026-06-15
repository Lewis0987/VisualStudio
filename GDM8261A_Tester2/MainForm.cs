using System;
using System.Drawing;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GDM8261A_Tester
{
    /// <summary>
    /// GW Instek GDM-8261A 六位半數位電錶測試操作介面。
    /// 支援 USB / RS-232 / LAN TCP。
    /// </summary>
    public partial class MainForm : Form
    {
        private IGdmTransport _transport;
        private readonly SemaphoreSlim _ioLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _pollCts;

        private RadioButton rbSerial;
        private RadioButton rbLan;
        private ComboBox cbPort;
        private ComboBox cbBaud;
        private Button btnRefreshPorts;
        private Button btnConnect;
        private Button btnDisconnect;
        private TextBox txtIp;
        private TextBox txtTcpPort;
        private Label lblStatus;
        private Label lblIdn;

        private ComboBox cbFunction;
        private ComboBox cbRange;
        private Button btnApply;
        private Button btnRead;
        private CheckBox chkContinuous;
        private NumericUpDown numInterval;
        private Label lblValue;

        private TextBox txtCmd;
        private Button btnWrite;
        private Button btnQuery;

        private RichTextBox txtLog;

        private class DmmFunction
        {
            public string Name;
            public string ConfCmd;
            public string Unit;
            public string[] Ranges;

            public override string ToString()
            {
                return Name;
            }
        }

        private static readonly DmmFunction[] Functions =
        {
            new DmmFunction { Name = "直流電壓 (DCV)", ConfCmd = "CONF:VOLT:DC", Unit = "V", Ranges = new[] { "AUTO", "0.1", "1", "10", "100", "1000" } },
            new DmmFunction { Name = "交流電壓 (ACV)", ConfCmd = "CONF:VOLT:AC", Unit = "V", Ranges = new[] { "AUTO", "0.1", "1", "10", "100", "750" } },
            new DmmFunction { Name = "直流電流 (DCI)", ConfCmd = "CONF:CURR:DC", Unit = "A", Ranges = new[] { "AUTO", "0.0001", "0.001", "0.01", "0.1", "1", "10" } },
            new DmmFunction { Name = "交流電流 (ACI)", ConfCmd = "CONF:CURR:AC", Unit = "A", Ranges = new[] { "AUTO", "0.0001", "0.001", "0.01", "0.1", "1", "10" } },
            new DmmFunction { Name = "二線電阻 (2W Ω)", ConfCmd = "CONF:RES", Unit = "Ω", Ranges = new[] { "AUTO", "100", "1E3", "10E3", "100E3", "1E6", "10E6", "100E6" } },
            new DmmFunction { Name = "四線電阻 (4W Ω)", ConfCmd = "CONF:FRES", Unit = "Ω", Ranges = new[] { "AUTO", "100", "1E3", "10E3", "100E3", "1E6", "10E6", "100E6" } },
            new DmmFunction { Name = "頻率 (FREQ)", ConfCmd = "CONF:FREQ", Unit = "Hz", Ranges = null },
            new DmmFunction { Name = "週期 (PERIOD)", ConfCmd = "CONF:PER", Unit = "s", Ranges = null },
            new DmmFunction { Name = "導通測試 (CONT)", ConfCmd = "CONF:CONT", Unit = "Ω", Ranges = null },
            new DmmFunction { Name = "二極體 (DIODE)", ConfCmd = "CONF:DIOD", Unit = "V", Ranges = null },
            new DmmFunction { Name = "溫度-熱電偶 (TEMP)", ConfCmd = "CONF:TEMP:TCO", Unit = "°C", Ranges = null },
        };

        public MainForm()
        {
            BuildUi();
            RefreshPorts();
            UpdateConnectionUi(false);
        }

        private void BuildUi()
        {
            Text = "GW Instek GDM-8261A 測試程式";
            ClientSize = new Size(984, 661);
            MinimumSize = new Size(1000, 540);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            Font = new Font("Microsoft JhengHei UI", 9F);
            FormClosing += MainForm_FormClosing;

            var gbConn = new GroupBox { Text = "連線設定", Location = new Point(10, 8), Size = new Size(964, 118), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(gbConn);

            rbSerial = new RadioButton { Text = "USB / RS-232", Location = new Point(15, 24), AutoSize = true, Checked = true };
            rbLan = new RadioButton { Text = "LAN (TCP)", Location = new Point(15, 56), AutoSize = true };
            rbSerial.CheckedChanged += (s, e) => UpdateInterfaceUi();
            rbLan.CheckedChanged += (s, e) => UpdateInterfaceUi();
            gbConn.Controls.Add(rbSerial);
            gbConn.Controls.Add(rbLan);

            cbPort = new ComboBox { Location = new Point(140, 22), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            btnRefreshPorts = new Button { Text = "重新整理", Location = new Point(246, 21), Size = new Size(75, 25) };
            btnRefreshPorts.Click += (s, e) => RefreshPorts();
            gbConn.Controls.Add(cbPort);
            gbConn.Controls.Add(btnRefreshPorts);

            gbConn.Controls.Add(new Label { Text = "鮑率:", Location = new Point(335, 26), AutoSize = true });
            cbBaud = new ComboBox { Location = new Point(375, 22), Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            cbBaud.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });
            cbBaud.SelectedItem = "115200";
            gbConn.Controls.Add(cbBaud);

            gbConn.Controls.Add(new Label { Text = "IP:", Location = new Point(140, 58), AutoSize = true });
            txtIp = new TextBox { Location = new Point(165, 54), Width = 120, Text = "192.168.0.100" };
            gbConn.Controls.Add(txtIp);

            gbConn.Controls.Add(new Label { Text = "Port:", Location = new Point(295, 58), AutoSize = true });
            txtTcpPort = new TextBox { Location = new Point(330, 54), Width = 55, Text = "3000" };
            gbConn.Controls.Add(txtTcpPort);

            btnConnect = new Button { Text = "連線", Location = new Point(700, 20), Size = new Size(120, 30) };
            btnConnect.Click += async (s, e) => await ConnectAsync();
            gbConn.Controls.Add(btnConnect);

            btnDisconnect = new Button { Text = "中斷連線", Location = new Point(830, 20), Size = new Size(120, 30) };
            btnDisconnect.Click += async (s, e) => await DisconnectAsync();
            gbConn.Controls.Add(btnDisconnect);

            lblStatus = new Label { Text = "● 未連線", Location = new Point(700, 58), AutoSize = true, ForeColor = Color.Firebrick };
            gbConn.Controls.Add(lblStatus);

            lblIdn = new Label { Text = "", Location = new Point(15, 88), Size = new Size(935, 20), ForeColor = Color.DimGray };
            gbConn.Controls.Add(lblIdn);

            var gbMeas = new GroupBox { Text = "量測功能", Location = new Point(10, 132), Size = new Size(470, 212), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            Controls.Add(gbMeas);

            gbMeas.Controls.Add(new Label { Text = "功能:", Location = new Point(15, 28), AutoSize = true });
            cbFunction = new ComboBox { Location = new Point(60, 24), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            cbFunction.Items.AddRange(Functions);
            cbFunction.SelectedIndex = 0;
            cbFunction.SelectedIndexChanged += (s, e) => UpdateRangeList();
            gbMeas.Controls.Add(cbFunction);

            gbMeas.Controls.Add(new Label { Text = "範圍:", Location = new Point(15, 62), AutoSize = true });
            cbRange = new ComboBox { Location = new Point(60, 58), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            gbMeas.Controls.Add(cbRange);
            UpdateRangeList();

            btnApply = new Button { Text = "套用功能", Location = new Point(300, 23), Size = new Size(150, 28) };
            btnApply.Click += async (s, e) => await ApplyFunctionAsync();
            gbMeas.Controls.Add(btnApply);

            btnRead = new Button { Text = "單次讀值 (READ?)", Location = new Point(300, 57), Size = new Size(150, 28) };
            btnRead.Click += async (s, e) => await SingleReadAsync();
            gbMeas.Controls.Add(btnRead);

            chkContinuous = new CheckBox { Text = "連續讀取，間隔", Location = new Point(15, 96), AutoSize = true };
            chkContinuous.CheckedChanged += ChkContinuous_CheckedChanged;
            gbMeas.Controls.Add(chkContinuous);

            numInterval = new NumericUpDown { Location = new Point(135, 94), Width = 70, Minimum = 100, Maximum = 60000, Increment = 100, Value = 500 };
            gbMeas.Controls.Add(numInterval);
            gbMeas.Controls.Add(new Label { Text = "ms", Location = new Point(208, 96), AutoSize = true });

            lblValue = new Label
            {
                Text = "-------",
                Location = new Point(15, 128),
                Size = new Size(435, 64),
                Font = new Font("Consolas", 26F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                BorderStyle = BorderStyle.FixedSingle
            };
            gbMeas.Controls.Add(lblValue);

            var gbCmd = new GroupBox { Text = "手動 SCPI 指令", Location = new Point(490, 132), Size = new Size(484, 212), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            Controls.Add(gbCmd);

            txtCmd = new TextBox { Location = new Point(15, 26), Width = 300, Text = "*IDN?" };
            gbCmd.Controls.Add(txtCmd);

            btnWrite = new Button { Text = "寫入", Location = new Point(325, 24), Size = new Size(65, 26) };
            btnWrite.Click += async (s, e) => await ManualWriteAsync();
            gbCmd.Controls.Add(btnWrite);

            btnQuery = new Button { Text = "查詢", Location = new Point(398, 24), Size = new Size(65, 26) };
            btnQuery.Click += async (s, e) => await ManualQueryAsync();
            gbCmd.Controls.Add(btnQuery);

            string[] quickCommands = { "*IDN?", "SYST:ERR?", "*RST", "*CLS", "READ?", "VAL1?", "VAL2?" };

            for (int i = 0; i < quickCommands.Length; i++)
            {
                var button = new Button
                {
                    Text = quickCommands[i],
                    Location = new Point(15 + (i % 4) * 115, 64 + (i / 4) * 34),
                    Size = new Size(108, 28),
                    Tag = quickCommands[i]
                };

                button.Click += async (s, e) =>
                {
                    string cmd = (string)((Button)s).Tag;
                    txtCmd.Text = cmd;

                    if (cmd.Contains("?"))
                    {
                        await ManualQueryAsync();
                    }
                    else
                    {
                        await ManualWriteAsync();
                    }
                };

                gbCmd.Controls.Add(button);
            }

            var hint = new Label
            {
                Text = "提示: 指令結尾含「?」用 [查詢]，其餘用 [寫入]。\n" +
                       "例: CONF:VOLT:DC 10 → 設定 DCV 10V 檔位\n" +
                       "完整指令請參考 GDM-8261A Programming 手冊。",
                Location = new Point(15, 136),
                Size = new Size(455, 68),
                ForeColor = Color.DimGray
            };
            gbCmd.Controls.Add(hint);

            var gbLog = new GroupBox { Text = "通訊紀錄", Location = new Point(10, 350), Size = new Size(964, 302), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(gbLog);

            txtLog = new RichTextBox
            {
                Location = new Point(15, 24),
                Size = new Size(934, 234),
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            gbLog.Controls.Add(txtLog);

            // 清除紀錄：置於「通訊紀錄」區塊右上角空白處
            var btnClear = new Button { Text = "清除紀錄", Location = new Point(844, 0), Size = new Size(105, 22), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnClear.Click += (s, e) =>
            {
                // 僅清除畫面上的通訊紀錄；不影響 LAN/USB Relay 連線、量測設定與測試結果
                if (MessageBox.Show(this, "確定要清除所有通訊紀錄？", "清除紀錄",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    txtLog.Clear();
                }
            };
            gbLog.Controls.Add(btnClear);
            btnClear.BringToFront();
        }

        private void RefreshPorts()
        {
            string current = cbPort.SelectedItem as string;

            cbPort.Items.Clear();

            foreach (string portName in SerialPort.GetPortNames())
            {
                cbPort.Items.Add(portName);
            }

            if (cbPort.Items.Count > 0)
            {
                cbPort.SelectedIndex = current != null && cbPort.Items.Contains(current)
                    ? cbPort.Items.IndexOf(current)
                    : 0;
            }
        }

        private async Task ConnectAsync()
        {
            if (_transport != null && _transport.IsOpen)
            {
                return;
            }

            try
            {
                if (rbSerial.Checked)
                {
                    if (cbPort.SelectedItem == null)
                    {
                        MessageBox.Show(
                            "找不到 COM 埠。請確認 USB 線已接上且已安裝驅動程式，再按 [重新整理]。",
                            "無 COM 埠",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        return;
                    }

                    _transport = new SerialTransport(
                        (string)cbPort.SelectedItem,
                        int.Parse((string)cbBaud.SelectedItem));
                }
                else
                {
                    string ip = txtIp.Text.Trim();
                    if (ip.Length == 0)
                    {
                        MessageBox.Show(
                            "請輸入儀器 IP 位址。",
                            "IP 未填",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        return;
                    }

                    if (!int.TryParse(txtTcpPort.Text.Trim(), out int tcpPort) ||
                        tcpPort < 1 || tcpPort > 65535)
                    {
                        MessageBox.Show(
                            "TCP Port 必須是 1~65535 的整數。",
                            "Port 格式錯誤",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        return;
                    }

                    _transport = new TcpTransport(ip, tcpPort);
                }

                btnConnect.Enabled = false;
                Log("SYS", "開啟連線: " + _transport.Description);

                // 整個連線流程（TryOpen + *IDN? 驗證）都不丟例外，
                // 失敗回傳錯誤訊息，避免偵錯器停在 throw、也不會跳到 VS 程式碼。
                var connect = await Task.Run<(bool ok, string idn, string error)>(() =>
                {
                    if (!_transport.TryOpen(out string openError))
                        return (false, null, openError);

                    // 不允許假連線：必須成功送出並收到 *IDN? 回覆，才算連線成功
                    try
                    {
                        _transport.WriteLine("*IDN?");
                        string reply = _transport.ReadLine();
                        if (string.IsNullOrWhiteSpace(reply))
                            return (false, null, "*IDN? 無回應，無法確認連線。");

                        return (true, reply, null);
                    }
                    catch (Exception ex)
                    {
                        return (false, null, ex.Message);
                    }
                });

                if (!connect.ok)
                {
                    Log("ERR", "連線失敗: " + connect.error);

                    _transport?.Dispose();
                    _transport = null;

                    UpdateConnectionUi(false);

                    // LAN 失敗用 DX01 風格的友善錯誤視窗；序列埠維持一般訊息
                    if (rbLan.Checked)
                        ShowLanError(connect.error);
                    else
                        MessageBox.Show(this, "連線失敗:\n" + connect.error, "錯誤",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);

                    return;
                }

                Log("→", "*IDN?");
                Log("←", connect.idn);
                lblIdn.Text = "儀器識別: " + connect.idn;

                UpdateConnectionUi(true);
                Log("SYS", "連線成功");
            }
            catch (Exception ex)
            {
                // 非預期例外的最後防線（理論上不會走到，連線失敗都走 connect.ok 分支）
                Log("ERR", "連線失敗: " + ex.Message);

                _transport?.Dispose();
                _transport = null;

                UpdateConnectionUi(false);

                if (rbLan.Checked)
                    ShowLanError(ex.Message);
                else
                    MessageBox.Show(this, "連線失敗:\n" + ex.Message, "錯誤",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>LAN 連線失敗的 DX01 風格錯誤視窗（友善排查指引 + 詳細錯誤）。</summary>
        private void ShowLanError(string detail)
        {
            MessageBox.Show(
                this,
                "無法連線至 GDM-8261A。\n\n" +
                "請確認：\n" +
                "1. LAN 線是否已接妥\n" +
                "2. IP / Port 是否正確\n" +
                "3. 電表 LAN 功能是否啟用\n" +
                "4. 等待 3 秒後再試一次\n\n" +
                "詳細錯誤：\n" + detail,
                "GDM LAN 連線失敗",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private async Task DisconnectAsync()
        {
            StopPolling();

            // 先把欄位清掉，避免之後的 I/O 取用到正在釋放的傳輸層
            IGdmTransport transport = _transport;
            _transport = null;

            if (transport != null)
            {
                // 取得 I/O 鎖，等待進行中的讀寫結束後才 Dispose，
                // 避免在 SerialPort.ReadLine 進行中關閉而造成 hang 或例外。
                await _ioLock.WaitAsync();
                try
                {
                    transport.Dispose();
                }
                catch
                {
                    // 關閉時例外可忽略
                }
                finally
                {
                    _ioLock.Release();
                }
            }

            lblIdn.Text = "";
            lblValue.Text = "-------";

            UpdateConnectionUi(false);
            Log("SYS", "已中斷連線");
        }

        private void UpdateConnectionUi(bool connected)
        {
            lblStatus.Text = connected ? "● 已連線" : "● 未連線";
            lblStatus.ForeColor = connected ? Color.Green : Color.Firebrick;

            btnConnect.Enabled = !connected;
            btnDisconnect.Enabled = connected;

            rbSerial.Enabled = !connected;
            rbLan.Enabled = !connected;

            btnApply.Enabled = connected;
            btnRead.Enabled = connected;
            chkContinuous.Enabled = connected;
            btnWrite.Enabled = connected;
            btnQuery.Enabled = connected;

            UpdateInterfaceUi();

            if (!connected && chkContinuous.Checked)
            {
                chkContinuous.Checked = false;
            }
        }

        private void UpdateInterfaceUi()
        {
            bool serial = rbSerial.Checked;
            bool connected = _transport != null && _transport.IsOpen;

            cbPort.Enabled = serial && !connected;
            btnRefreshPorts.Enabled = serial && !connected;
            cbBaud.Enabled = serial && !connected;

            txtIp.Enabled = !serial && !connected;
            txtTcpPort.Enabled = !serial && !connected;
        }

        private void UpdateRangeList()
        {
            var fn = (DmmFunction)cbFunction.SelectedItem;

            cbRange.Items.Clear();

            if (fn.Ranges == null)
            {
                cbRange.Items.Add("(無)");
                cbRange.Enabled = false;
            }
            else
            {
                cbRange.Items.AddRange(fn.Ranges);
                cbRange.Enabled = true;
            }

            cbRange.SelectedIndex = 0;
        }

        private async Task ApplyFunctionAsync()
        {
            var fn = (DmmFunction)cbFunction.SelectedItem;
            string range = cbRange.SelectedItem as string;

            string cmd = fn.ConfCmd;

            if (fn.Ranges != null && range != "AUTO")
            {
                cmd += " " + range;
            }

            await SendAsync(cmd);
        }

        private async Task SingleReadAsync()
        {
            string resp = await QueryAsync("READ?");
            ShowValue(resp);
        }

        private void ShowValue(string raw)
        {
            if (raw == null)
            {
                return;
            }

            string first = raw.Split(',')[0].Trim();

            double value;
            if (!double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                lblValue.Text = first;
                return;
            }

            var fn = (DmmFunction)cbFunction.SelectedItem;

            if (Math.Abs(value) >= 9.9e37)
            {
                lblValue.Text = "OL  " + fn.Unit;
            }
            else
            {
                lblValue.Text = FormatEng(value) + " " + fn.Unit;
            }
        }

        private static string FormatEng(double value)
        {
            double abs = Math.Abs(value);

            if (abs == 0)
            {
                return "0.000000";
            }

            if (abs >= 1e6)
            {
                return (value / 1e6).ToString("0.000000") + " M";
            }

            if (abs >= 1e3)
            {
                return (value / 1e3).ToString("0.000000") + " k";
            }

            if (abs >= 1)
            {
                return value.ToString("0.000000") + " ";
            }

            if (abs >= 1e-3)
            {
                return (value * 1e3).ToString("0.000000") + " m";
            }

            if (abs >= 1e-6)
            {
                return (value * 1e6).ToString("0.000000") + " µ";
            }

            return value.ToString("0.000E+00") + " ";
        }

        private async void ChkContinuous_CheckedChanged(object sender, EventArgs e)
        {
            if (chkContinuous.Checked)
            {
                btnRead.Enabled = false;
                _pollCts = new CancellationTokenSource();

                await PollLoopAsync(_pollCts.Token);
            }
            else
            {
                StopPolling();
                btnRead.Enabled = _transport != null && _transport.IsOpen;
            }
        }

        private void StopPolling()
        {
            _pollCts?.Cancel();
            _pollCts = null;
        }

        private async Task PollLoopAsync(CancellationToken token)
        {
            Log("SYS", "開始連續讀取");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    string resp = await QueryAsync("READ?", logTraffic: false);

                    // 讀取期間若已要求停止，丟棄這筆過期結果，避免停止後數值再跳動
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    if (resp == null)
                    {
                        break;
                    }

                    ShowValue(resp);

                    await Task.Delay((int)numInterval.Value, token);
                }
            }
            catch (TaskCanceledException)
            {
                // 正常停止
            }

            Log("SYS", "停止連續讀取");

            if (chkContinuous.Checked)
            {
                chkContinuous.Checked = false;
            }
        }

        private async Task ManualWriteAsync()
        {
            string cmd = txtCmd.Text.Trim();

            if (cmd.Length == 0)
            {
                return;
            }

            await SendAsync(cmd);
        }

        private async Task ManualQueryAsync()
        {
            string cmd = txtCmd.Text.Trim();

            if (cmd.Length == 0)
            {
                return;
            }

            string resp = await QueryAsync(cmd);

            if (resp != null &&
                (cmd.ToUpperInvariant().StartsWith("READ") ||
                 cmd.ToUpperInvariant().StartsWith("VAL")))
            {
                ShowValue(resp);
            }
        }

        private async Task<bool> SendAsync(string cmd)
        {
            if (!EnsureConnected())
            {
                return false;
            }

            await _ioLock.WaitAsync();

            try
            {
                // 在鎖內抓取穩定參考，避免背景執行緒在斷線瞬間踩到 null
                IGdmTransport transport = _transport;
                if (transport == null)
                {
                    Log("ERR", "尚未連線");
                    return false;
                }

                await Task.Run(() => transport.WriteLine(cmd));
                Log("→", cmd);
                return true;
            }
            catch (Exception ex)
            {
                DropConnection(cmd + " 寫入", ex);
                return false;
            }
            finally
            {
                _ioLock.Release();
            }
        }

        private async Task<string> QueryAsync(string cmd, bool logTraffic = true)
        {
            if (!EnsureConnected())
            {
                return null;
            }

            await _ioLock.WaitAsync();

            try
            {
                // 在鎖內抓取穩定參考，避免背景執行緒在斷線瞬間踩到 null
                IGdmTransport transport = _transport;
                if (transport == null)
                {
                    Log("ERR", "尚未連線");
                    return null;
                }

                string resp = await Task.Run(() =>
                {
                    transport.WriteLine(cmd);
                    return transport.ReadLine();
                });

                if (logTraffic)
                {
                    Log("→", cmd);
                    Log("←", resp);
                }

                return resp;
            }
            catch (Exception ex)
            {
                DropConnection(cmd + " 查詢", ex);
                return null;
            }
            finally
            {
                _ioLock.Release();
            }
        }

        private bool EnsureConnected()
        {
            if (_transport != null && _transport.IsOpen)
            {
                return true;
            }

            Log("ERR", "尚未連線");
            return false;
        }

        /// <summary>
        /// 任一階段 I/O 失敗時呼叫：立即釋放連線、停止連續讀取、UI 改為未連線，
        /// 並記錄一次錯誤 + 跳出錯誤訊息（已斷線則不重複處理，避免洗版 / 重複跳窗）。
        /// </summary>
        private void DropConnection(string context, Exception ex)
        {
            if (_transport == null)
                return; // 已斷線，避免重複

            Log("ERR", context + " 失敗: " + ex.Message);

            StopPolling();

            IGdmTransport transport = _transport;
            _transport = null;
            try { transport.Dispose(); }
            catch { }

            UpdateConnectionUi(false);

            MessageBox.Show(
                this,
                "通訊失敗，連線已中斷:\n" + ex.Message,
                "錯誤",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private void Log(string tag, string msg)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Log(tag, msg)));
                return;
            }

            if (txtLog.Lines.Length > 2000)
            {
                // 只移除最舊的前半段，保留近期紀錄與其顏色，不整個清空
                int removeCount = txtLog.Lines.Length / 2;
                int charIndex = txtLog.GetFirstCharIndexFromLine(removeCount);

                if (charIndex > 0)
                {
                    txtLog.Select(0, charIndex);
                    txtLog.SelectedText = "";
                }
            }

            Color color;

            switch (tag)
            {
                case "→":
                    color = Color.Blue;
                    break;

                case "←":
                    color = Color.DarkGreen;
                    break;

                case "ERR":
                    color = Color.Red;
                    break;

                default:
                    color = Color.Gray;
                    break;
            }

            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionColor = color;
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {tag} {msg}\r\n");
            txtLog.ScrollToCaret();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopPolling();

            IGdmTransport transport = _transport;
            _transport = null;

            if (transport == null)
            {
                return;
            }

            // 程式關閉，盡量等進行中的 I/O 結束再釋放（最多 2 秒，逾時則強制釋放）
            bool acquired = _ioLock.Wait(2000);
            try
            {
                transport.Dispose();
            }
            catch
            {
                // 關閉時例外可忽略
            }
            finally
            {
                if (acquired)
                {
                    // 確定已無進行中的 I/O 才釋放並 Dispose，避免背景續行踩到已釋放的鎖
                    _ioLock.Release();
                    _ioLock.Dispose();
                }
            }
        }
    }
}
