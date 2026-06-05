
using GDM8261A_Tester;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Runtime.Remoting.Lifetime;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace GDM8261A_Tester
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}

---
3️⃣ GdmTransport.cs — 通訊層

using System;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;

namespace GDM8261A_Tester
{
    /// <summary>
    /// GDM-8261A 通訊介面抽象層。
    /// USB(虛擬 COM 埠) / RS-232 走 SerialTransport，LAN 走 TcpTransport。
    /// SCPI 指令以 "\n" 結尾，回應同樣以換行結尾。
    /// </summary>
    public interface IGdmTransport : IDisposable
    {
        bool IsOpen { get; }
        string Description { get; }
        void Open();
        void Close();
        void WriteLine(string command);
        string ReadLine();
    }

    /// <summary>USB(虛擬 COM) / RS-232 序列埠通訊</summary>
    public class SerialTransport : IGdmTransport
    {
        private readonly SerialPort _port;

        public SerialTransport(string portName, int baudRate)
        {
            _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                NewLine = "\n",
                ReadTimeout = 3000,
                WriteTimeout = 3000,
                DtrEnable = true,   // USB CDC 虛擬 COM 埠通常需要 DTR
                RtsEnable = true,
                Encoding = Encoding.ASCII
            };
        }

        public bool IsOpen => _port.IsOpen;
        public string Description => $"{_port.PortName} @ {_port.BaudRate} bps";

        public void Open()
        {
            _port.Open();
            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();
        }

        public void Close()
        {
            if (_port.IsOpen) _port.Close();
        }

        public void WriteLine(string command)
        {
            _port.WriteLine(command);
        }

        public string ReadLine()
        {
            // SerialPort.ReadLine 以 NewLine("\n") 為結尾，移除可能殘留的 '\r'
            return _port.ReadLine().TrimEnd('\r');
        }

        public void Dispose()
        {
            Close();
            _port.Dispose();
        }
    }

    /// <summary>LAN (TCP socket) 通訊，GDM-8261A 出廠預設 port 3000</summary>
    public class TcpTransport : IGdmTransport
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient _client;
        private NetworkStream _stream;

        public TcpTransport(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public bool IsOpen => _client != null && _client.Connected;
        public string Description => $"{_host}:{_port}";

        public void Open()
        {
            _client = new TcpClient();
            var result = _client.BeginConnect(_host, _port, null, null);
            if (!result.AsyncWaitHandle.WaitOne(3000))
            {
                _client.Close();
                throw new TimeoutException($"連線 {_host}:{_port} 逾時");
            }
            _client.EndConnect(result);
            _stream = _client.GetStream();
            _stream.ReadTimeout = 3000;
            _stream.WriteTimeout = 3000;
        }

        public void Close()
        {
            _stream?.Close();
            _client?.Close();
            _stream = null;
            _client = null;
        }

        public void WriteLine(string command)
        {
            byte[] data = Encoding.ASCII.GetBytes(command + "\n");
            _stream.Write(data, 0, data.Length);
        }

        public string ReadLine()
        {
            // 逐 byte 讀到 '\n' 為止（量測值回應都很短，效能足夠）
            var sb = new StringBuilder(64);
            while (true)
            {
                int b = _stream.ReadByte();
                if (b < 0) throw new IOException("連線已被遠端關閉");
                if (b == '\n') break;
                if (b != '\r') sb.Append((char)b);
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            Close();
        }
    }
}

---
4️⃣ MainForm.cs — 主視窗

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
    /// GW Instek GDM-8261A 六位半數位電錶 測試操作介面
    /// 支援 USB(虛擬COM)/RS-232 與 LAN(TCP, 預設 port 3000)
    /// </summary>
    public class MainForm : Form
    {
        // ===== 通訊 =====
        private IGdmTransport _transport;
        private readonly SemaphoreSlim _ioLock = new SemaphoreSlim(1, 1); // 序列化所有指令，避免連續讀取與按鈕操作互相干擾
        private CancellationTokenSource _pollCts;

        // ===== 連線設定 =====
        private RadioButton rbSerial, rbLan;
        private ComboBox cbPort, cbBaud;
        private Button btnRefreshPorts, btnConnect, btnDisconnect;
        private TextBox txtIp, txtTcpPort;
        private Label lblStatus, lblIdn;

        // ===== 量測功能 =====
        private ComboBox cbFunction, cbRange;
        private Button btnApply, btnRead;
        private CheckBox chkContinuous;
        private NumericUpDown numInterval;
        private Label lblValue;

        // ===== 手動 SCPI =====
        private TextBox txtCmd;
        private Button btnWrite, btnQuery;

        // ===== 紀錄 =====
        private RichTextBox txtLog;

        /// <summary>量測功能定義（CONF 指令、單位、可用範圍）</summary>
        private class DmmFunction
        {
            public string Name;
            public string ConfCmd;
            public string Unit;
            public string[] Ranges;   // SCPI 範圍參數；null 表示無範圍選項
            public override string ToString() { return Name; }
        }

        private static readonly DmmFunction[] Functions =
        {
            new DmmFunction { Name = "直流電壓 (DCV)",      ConfCmd = "CONF:VOLT:DC", Unit = "V",  Ranges = new[]{ "AUTO", "0.1", "1", "10", "100", "1000" } },
            new DmmFunction { Name = "交流電壓 (ACV)",      ConfCmd = "CONF:VOLT:AC", Unit = "V",  Ranges = new[]{ "AUTO", "0.1", "1", "10", "100", "750" } },
            new DmmFunction { Name = "直流電流 (DCI)",      ConfCmd = "CONF:CURR:DC", Unit = "A",  Ranges = new[]{ "AUTO", "0.0001", "0.001", "0.01", "0.1", "1", "10" } },
            new DmmFunction { Name = "交流電流 (ACI)",      ConfCmd = "CONF:CURR:AC", Unit = "A",  Ranges = new[]{ "AUTO", "0.0001", "0.001", "0.01", "0.1", "1", "10" } },
            new DmmFunction { Name = "二線電阻 (2W Ω)",     ConfCmd = "CONF:RES",     Unit = "Ω",  Ranges = new[]{ "AUTO", "100", "1E3", "10E3", "100E3", "1E6", "10E6", "100E6" } },
            new DmmFunction { Name = "四線電阻 (4W Ω)",     ConfCmd = "CONF:FRES",    Unit = "Ω",  Ranges = new[]{ "AUTO", "100", "1E3", "10E3", "100E3", "1E6", "10E6", "100E6" } },
            new DmmFunction { Name = "頻率 (FREQ)",         ConfCmd = "CONF:FREQ",    Unit = "Hz", Ranges = null },
            new DmmFunction { Name = "週期 (PERIOD)",       ConfCmd = "CONF:PER",     Unit = "s",  Ranges = null },
            new DmmFunction { Name = "導通測試 (CONT)",     ConfCmd = "CONF:CONT",    Unit = "Ω",  Ranges = null },
            new DmmFunction { Name = "二極體 (DIODE)",      ConfCmd = "CONF:DIOD",    Unit = "V",  Ranges = null },
            new DmmFunction { Name = "溫度-熱電偶 (TEMP)",  ConfCmd = "CONF:TEMP:TCO", Unit = "°C", Ranges = null },
        };

        public MainForm()
        {
            BuildUi();
            RefreshPorts();
            UpdateConnectionUi(false);
        }

        // =====================================================================
        //  UI 建立
        // =====================================================================
        private void BuildUi()
        {
            Text = "GW Instek GDM-8261A 測試程式";
            ClientSize = new Size(984, 661);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Font = new Font("Microsoft JhengHei UI", 9F);
            FormClosing += MainForm_FormClosing;

            // ---------- 連線設定 ----------
            var gbConn = new GroupBox { Text = "連線設定", Location = new Point(10, 8), Size = new Size(964, 118) };
            Controls.Add(gbConn);

            rbSerial = new RadioButton { Text = "USB / RS-232", Location = new Point(15, 24), AutoSize = true, Checked = true };
            rbLan = new RadioButton { Text = "LAN (TCP)", Location = new Point(15, 56), AutoSize = true };
            rbSerial.CheckedChanged += (s, e) => UpdateInterfaceUi();
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
            btnDisconnect = new Button { Text = "中斷連線", Location = new Point(830, 20), Size = new Size(120, 30) };
            btnDisconnect.Click += (s, e) => Disconnect();
            gbConn.Controls.Add(btnConnect);
            gbConn.Controls.Add(btnDisconnect);

            lblStatus = new Label { Text = "● 未連線", Location = new Point(700, 58), AutoSize = true, ForeColor = Color.Firebrick };
            gbConn.Controls.Add(lblStatus);

            lblIdn = new Label { Text = "", Location = new Point(15, 88), Size = new Size(935, 20), ForeColor = Color.DimGray };
            gbConn.Controls.Add(lblIdn);

            // ---------- 量測功能 ----------
            var gbMeas = new GroupBox { Text = "量測功能", Location = new Point(10, 132), Size = new Size(470, 212) };
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
            btnRead = new Button { Text = "單次讀值 (READ?)", Location = new Point(300, 57), Size = new Size(150, 28) };
            btnRead.Click += async (s, e) => await SingleReadAsync();
            gbMeas.Controls.Add(btnApply);
            gbMeas.Controls.Add(btnRead);

            chkContinuous = new CheckBox { Text = "連續讀取，間隔", Location = new Point(15, 96), AutoSize = true };
            chkContinuous.CheckedChanged += ChkContinuous_CheckedChanged;
            numInterval = new NumericUpDown { Location = new Point(135, 94), Width = 70, Minimum = 100, Maximum = 60000, Increment = 100, Value = 500 };
            gbMeas.Controls.Add(chkContinuous);
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

            // ---------- 手動 SCPI 指令 ----------
            var gbCmd = new GroupBox { Text = "手動 SCPI 指令", Location = new Point(490, 132), Size = new Size(484, 212) };
            Controls.Add(gbCmd);

            txtCmd = new TextBox { Location = new Point(15, 26), Width = 300, Text = "*IDN?" };
            btnWrite = new Button { Text = "寫入", Location = new Point(325, 24), Size = new Size(65, 26) };
            btnWrite.Click += async (s, e) => await ManualWriteAsync();
            btnQuery = new Button { Text = "查詢", Location = new Point(398, 24), Size = new Size(65, 26) };
            btnQuery.Click += async (s, e) => await ManualQueryAsync();
            gbCmd.Controls.Add(txtCmd);
            gbCmd.Controls.Add(btnWrite);
            gbCmd.Controls.Add(btnQuery);

            // 常用指令快捷鈕
            string[] quick = { "*IDN?", "SYST:ERR?", "*RST", "*CLS", "READ?", "VAL1?", "VAL2?" };
            for (int i = 0; i < quick.Length; i++)
            {
                var b = new Button
                {
                    Text = quick[i],
                    Location = new Point(15 + (i % 4) * 115, 64 + (i / 4) * 34),
                    Size = new Size(108, 28),
                    Tag = quick[i]
                };
                b.Click += async (s, e) =>
                {
                    string cmd = (string)((Button)s).Tag;
                    txtCmd.Text = cmd;
                    if (cmd.Contains("?")) await ManualQueryAsync();
                    else await ManualWriteAsync();
                };
                gbCmd.Controls.Add(b);
            }

            var hint = new Label
            {
                Text = "提示: 指令結尾含「?」用[查詢]，其餘用[寫入]。\n" +
                       "例: CONF:VOLT:DC 10 → 設定 DCV 10V 檔位\n" +
                       "      SENS:DET:RATE S → 取樣速率 Slow\n" +
                       "完整指令請參考 GDM-8261A Programming 手冊章節。",
                Location = new Point(15, 136),
                Size = new Size(455, 68),
                ForeColor = Color.DimGray
            };
            gbCmd.Controls.Add(hint);

            // ---------- 通訊紀錄 ----------
            var gbLog = new GroupBox { Text = "通訊紀錄", Location = new Point(10, 350), Size = new Size(964, 302) };
            Controls.Add(gbLog);

            txtLog = new RichTextBox
            {
                Location = new Point(15, 24),
                Size = new Size(934, 234),
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 9F)
            };
            gbLog.Controls.Add(txtLog);

            var btnClear = new Button { Text = "清除紀錄", Location = new Point(849, 264), Size = new Size(100, 28) };
            btnClear.Click += (s, e) => txtLog.Clear();
            gbLog.Controls.Add(btnClear);
        }

        // =====================================================================
        //  連線 / 中斷
        // =====================================================================
        private void RefreshPorts()
        {
            string current = cbPort.SelectedItem as string;
            cbPort.Items.Clear();
            foreach (string p in SerialPort.GetPortNames())
                cbPort.Items.Add(p);
            if (cbPort.Items.Count > 0)
                cbPort.SelectedIndex = current != null && cbPort.Items.Contains(current)
                    ? cbPort.Items.IndexOf(current) : 0;
        }

        private async Task ConnectAsync()
        {
            if (_transport != null && _transport.IsOpen) return;

            try
            {
                if (rbSerial.Checked)
                {
                    if (cbPort.SelectedItem == null)
                    {
                        MessageBox.Show("找不到 COM 埠。請確認 USB 線已接上且已安裝 GDM-8261A 的 USB 驅動程式，再按 [重新整理]。",
                            "無 COM 埠", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    _transport = new SerialTransport((string)cbPort.SelectedItem, int.Parse((string)cbBaud.SelectedItem));
                }
                else
                {
                    _transport = new TcpTransport(txtIp.Text.Trim(), int.Parse(txtTcpPort.Text.Trim()));
                }

                btnConnect.Enabled = false;
                Log("SYS", "開啟連線: " + _transport.Description);
                await Task.Run(() => _transport.Open());

                // 連線後先查詢機器識別，確認通訊正常
                string idn = await QueryAsync("*IDN?");
                lblIdn.Text = "儀器識別: " + idn;

                UpdateConnectionUi(true);
                Log("SYS", "連線成功");
            }
            catch (Exception ex)
            {
                Log("ERR", "連線失敗: " + ex.Message);
                _transport?.Dispose();
                _transport = null;
                UpdateConnectionUi(false);
                MessageBox.Show("連線失敗:\n" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Disconnect()
        {
            StopPolling();
            try { _transport?.Dispose(); }
            catch { /* 關閉時的例外可忽略 */ }
            _transport = null;
            lblIdn.Text = "";
            UpdateConnectionUi(false);
            Log("SYS", "已中斷連線");
        }

        private void UpdateConnectionUi(bool connected)
        {
            lblStatus.Text = connected ? "● 已連線" : "● 未連線";
            lblStatus.ForeColor = connected ? Color.Green : Color.Firebrick;
            btnConnect.Enabled = !connected;
            btnDisconnect.Enabled = connected;
            rbSerial.Enabled = rbLan.Enabled = !connected;
            btnApply.Enabled = btnRead.Enabled = chkContinuous.Enabled = connected;
            btnWrite.Enabled = btnQuery.Enabled = connected;
            UpdateInterfaceUi();
            if (!connected && chkContinuous.Checked) chkContinuous.Checked = false;
        }

        private void UpdateInterfaceUi()
        {
            bool serial = rbSerial.Checked;
            bool connected = _transport != null && _transport.IsOpen;
            cbPort.Enabled = btnRefreshPorts.Enabled = cbBaud.Enabled = serial && !connected;
            txtIp.Enabled = txtTcpPort.Enabled = !serial && !connected;
        }

        // =====================================================================
        //  量測
        // =====================================================================
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

        /// <summary>送出 CONF 指令切換量測功能/範圍</summary>
        private async Task ApplyFunctionAsync()
        {
            var fn = (DmmFunction)cbFunction.SelectedItem;
            string range = cbRange.SelectedItem as string;

            string cmd = fn.ConfCmd;
            if (fn.Ranges != null && range != "AUTO")
                cmd += " " + range;          // 不帶參數 = 自動範圍

            await SendAsync(cmd);
        }

        private async Task SingleReadAsync()
        {
            string resp = await QueryAsync("READ?");
            ShowValue(resp);
        }

        /// <summary>把儀器回傳的科學記號字串顯示到大字幕</summary>
        private void ShowValue(string raw)
        {
            if (raw == null) return;
            string first = raw.Split(',')[0].Trim();   // SAMP:COUN > 1 時會回多筆，取第一筆

            double v;
            if (!double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            {
                lblValue.Text = first;
                return;
            }

            var fn = (DmmFunction)cbFunction.SelectedItem;
            if (Math.Abs(v) >= 9.9e37)                 // 儀器以 +9.9E+37 表示過載
                lblValue.Text = "OL  " + fn.Unit;
            else
                lblValue.Text = FormatEng(v) + " " + fn.Unit;
        }

        /// <summary>工程記號格式化 (m / µ / k / M)</summary>
        private static string FormatEng(double v)
        {
            double abs = Math.Abs(v);
            if (abs == 0) return "0.000000";
            if (abs >= 1e6) return (v / 1e6).ToString("0.000000") + " M";
            if (abs >= 1e3) return (v / 1e3).ToString("0.000000") + " k";
            if (abs >= 1) return v.ToString("0.000000") + " ";
            if (abs >= 1e-3) return (v * 1e3).ToString("0.000000") + " m";
            if (abs >= 1e-6) return (v * 1e6).ToString("0.000000") + " µ";
            return v.ToString("0.000E+00") + " ";
        }

        // =====================================================================
        //  連續讀取
        // =====================================================================
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
                    if (resp == null) break;            // 通訊失敗 → 結束輪詢
                    ShowValue(resp);
                    await Task.Delay((int)numInterval.Value, token);
                }
            }
            catch (TaskCanceledException) { /* 正常停止 */ }
            Log("SYS", "停止連續讀取");
            if (chkContinuous.Checked) chkContinuous.Checked = false;
        }

        // =====================================================================
        //  手動指令
        // =====================================================================
        private async Task ManualWriteAsync()
        {
            string cmd = txtCmd.Text.Trim();
            if (cmd.Length == 0) return;
            await SendAsync(cmd);
        }

        private async Task ManualQueryAsync()
        {
            string cmd = txtCmd.Text.Trim();
            if (cmd.Length == 0) return;
            string resp = await QueryAsync(cmd);
            if (resp != null && cmd.ToUpperInvariant().StartsWith("READ") ||
                resp != null && cmd.ToUpperInvariant().StartsWith("VAL"))
                ShowValue(resp);
        }

        // =====================================================================
        //  低階收發（皆在背景執行緒執行，並以 _ioLock 序列化）
        // =====================================================================
        /// <summary>只寫入不讀回。失敗回傳 false。</summary>
        private async Task<bool> SendAsync(string cmd)
        {
            if (!EnsureConnected()) return false;
            await _ioLock.WaitAsync();
            try
            {
                await Task.Run(() => _transport.WriteLine(cmd));
                Log("→", cmd);
                return true;
            }
            catch (Exception ex)
            {
                Log("ERR", cmd + " 寫入失敗: " + ex.Message);
                return false;
            }
            finally { _ioLock.Release(); }
        }

        /// <summary>寫入並讀回一行。失敗回傳 null。</summary>
        private async Task<string> QueryAsync(string cmd, bool logTraffic = true)
        {
            if (!EnsureConnected()) return null;
            await _ioLock.WaitAsync();
            try
            {
                string resp = await Task.Run(() =>
                {
                    _transport.WriteLine(cmd);
                    return _transport.ReadLine();
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
                Log("ERR", cmd + " 查詢失敗: " + ex.Message);
                return null;
            }
            finally { _ioLock.Release(); }
        }

        private bool EnsureConnected()
        {
            if (_transport != null && _transport.IsOpen) return true;
            Log("ERR", "尚未連線");
            return false;
        }

        // =====================================================================
        //  紀錄
        // =====================================================================
        private void Log(string tag, string msg)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Log(tag, msg)));
                return;
            }
            // 避免紀錄無限成長
            if (txtLog.Lines.Length > 2000) txtLog.Clear();

            Color c;
            switch (tag)
            {
                case "→": c = Color.Blue; break;
                case "←": c = Color.DarkGreen; break;
                case "ERR": c = Color.Red; break;
                default: c = Color.Gray; break;
            }
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionColor = c;

        // =====================================================================
        //  紀錄
        // =====================================================================
        private void Log(string tag, string msg)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Log(tag, msg)));
                return;
            }
            // 避免紀錄無限成長
            if (txtLog.Lines.Length > 2000) txtLog.Clear();

            Color c;
            switch (tag)
            {
                case "→": c = Color.Blue; break;
                case "←": c = Color.DarkGreen; break;
                case "ERR": c = Color.Red; break;
                default: c = Color.Gray; break;
            }
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionColor = c;
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {tag} {msg}\r\n");
            txtLog.ScrollToCaret();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopPolling();
            try { _transport?.Dispose(); } catch { }
        }
    }
}
EndGlobalSection
      GlobalSection(ProjectConfigurationPlatforms) = postSolution
              { B5E1A9D3 - 7C42 - 4F18 - 9A66 - 2D8E0C5F31AB}.Debug | Any CPU.ActiveCfg = Debug | Any CPU
              {B5E1A9D3-7C42-4F18-9A66-2D8E0C5F31AB}.Debug | Any CPU.Build.0 = Debug | Any CPU
              {B5E1A9D3-7C42-4F18-9A66-2D8E0C5F31AB}.Release | Any CPU.ActiveCfg = Release | Any CPU
              {B5E1A9D3-7C42-4F18-9A66-2D8E0C5F31AB}.R