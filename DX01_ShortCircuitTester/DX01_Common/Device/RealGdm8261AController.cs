using System;
using System.Globalization;
using System.Text;
using System.Threading;
using DX01_ShortCircuitTester.Services;
// 連線模式以 AppSettings.Current.ConnectionMode 為單一來源

namespace DX01_ShortCircuitTester.Device
{
    /// <summary>
    /// 真實 GDM-8261A 電表控制器。
    /// 通訊重用 <see cref="SerialTransport"/>（來自 GDM8261A_Tester 專案），
    /// SCPI 指令樣式沿用該專案 MainForm 的用法（CONF:RES / CONF:VOLT:DC / READ?）。
    /// 每次送出 / 回傳 / 錯誤都寫入 <see cref="DebugLog"/> 供實機驗證觀察。
    /// </summary>
    public sealed class RealGdm8261AController : IGdm8261AController
    {
        private IGdmTransport _transport;
        private MeasurementMode _mode = MeasurementMode.Resistance;

        /// <summary>序列埠名稱（例如 COM3）。</summary>
        public string PortName { get; set; }

        /// <summary>鮑率，預設 115200。</summary>
        public int BaudRate { get; set; } = 115200;

        /// <summary>
        /// true = 使用 LAN(TCP)；false = 使用序列埠。
        /// 代理 GdmConnectionConfig.Mode，使連線模式為單一來源（MainForm 設定此值即更新設定）。
        /// </summary>
        public bool UseLan
        {
            get { return AppSettings.Current.ConnectionMode == GdmConnectionMode.Lan; }
            set { AppSettings.Current.ConnectionMode = value ? GdmConnectionMode.Lan : GdmConnectionMode.Serial; }
        }

        /// <summary>LAN 連線 IP，預設 192.168.100.100。</summary>
        public string Ip { get; set; } = "192.168.100.100";

        /// <summary>LAN 連線 Port，預設 23。</summary>
        public int TcpPort { get; set; } = 23;

        /// <summary>連線後讀回的 *IDN? 機型識別字串。</summary>
        public string Idn { get; private set; }

        /// <summary>除錯日誌（可為 null）。</summary>
        public DebugLog Log { get; set; }

        /// <summary>
        /// 已連線狀態的連線「中途遺失」事件（通訊 I/O 失敗判定斷線時觸發）。
        /// 連線建立階段（含 *IDN? 驗證失敗）不觸發，避免與連線失敗流程重複。
        /// 供 UI 即時更新「電表：未連線」與寫 Debug Log。
        /// </summary>
        public event EventHandler ConnectionLost;

        private bool _connecting;   // 連線建立中：此期間的釋放不視為「中途斷線」

        // 連線中途遺失：標記為斷線但「不立即 Dispose」舊 socket。
        // 原因（比照 GDM8261A_Tester2）：若在「線材仍拔除」時 Dispose，FIN 送不到 GDM，
        // GDM 端舊 session 殘留，重連時新連線會被 RST（TX *CLS 失敗 / 遠端強制關閉）。
        // 保留舊 socket，待重連 / 中斷時（線材已接回）才 Dispose → FIN 送達 → GDM 釋放舊 session。
        private bool _linkLost;

        // I/O 序列化鎖：背景心跳輪詢與 UI 操作（連線/中斷/測試指令）不會同時存取 _transport。
        // lock 為可重入（同執行緒），故 Connect→Send/Query 巢狀取用安全。
        private readonly object _ioGate = new object();

        public bool IsConnected
        {
            get { return !_linkLost && _transport != null && _transport.IsOpen; }
        }

        public void Connect()
        {
            lock (_ioGate)
            {
                if (IsConnected)
                    return;

                // 不沿用任何殘留 / 失效的舊連線物件（拔線後 TcpClient 可能仍非 null）。
                CloseTransport();

                _connecting = true;
                try
                {
                    if (AppSettings.Current.ConnectionMode == GdmConnectionMode.Lan)
                        ConnectLan();
                    else
                        ConnectSerial();
                }
                finally
                {
                    _connecting = false;
                }
            }
        }

        /// <summary>
        /// LAN 重新連線：完整 Close 舊連線 → 等待 → new TcpClient → 連線(帶 timeout)
        /// → 清空殘留 → *CLS → *IDN? 驗證；任一步失敗則 Close 並重試（共 ReconnectRetryCount 次）。
        /// 全部失敗才丟出例外（不需重開電表）。
        /// </summary>
        private void ConnectLan()
        {
            if (string.IsNullOrEmpty(Ip))
                throw new InvalidOperationException("尚未輸入 IP 位址。");

            var cfg = AppSettings.Current;
            int retries = Math.Max(1, cfg.ReconnectRetryCount);
            int delay = Math.Max(500, cfg.ReconnectDelayMs);   // #3 連線前延遲（≥500ms）
            Exception last = null;

            for (int attempt = 1; attempt <= retries; attempt++)
            {
                CloseTransport();           // 不沿用任何舊 Stream / TcpClient（每次全新）
                Thread.Sleep(delay);        // #3 Disconnect 後 / 重試前延遲，待 GDM 釋放舊 session

                try
                {
                    // 建立全新 TcpClient 並連線（帶 ConnectTimeoutMs）
                    var t = new TcpTransport(Ip, TcpPort, cfg.ConnectTimeoutMs, cfg.ReadTimeoutMs);
                    t.Open();
                    _transport = t;
                    WriteLog(LogKind.Info, "TcpClient Connect OK  LAN " + Ip + ":" + TcpPort +
                        "  (retry " + attempt + "/" + retries + ")");

                    // #4 連線後稍候再送第一個 SCPI，待 GDM 端就緒
                    Thread.Sleep(400);

                    // #1 #5 比照 GDM8261A_Tester2：不送 *CLS、不 DrainInput，直接以 *IDN? 驗證
                    VerifyIdentity("LAN");
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    WriteLog(LogKind.Error, "LAN Connect Failed (retry " + attempt + "/" + retries + "): " + ex.Message);
                    CloseTransport();           // #6 失敗立即完整釋放全部 TCP 資源
                    delay = Math.Max(1000, delay); // #6 首次失敗後延長延遲（≥1000ms）再以全新連線重試
                }
            }

            throw new InvalidOperationException(
                "GDM LAN 連線失敗（已重試 " + retries + " 次）：" +
                (last != null ? last.Message : "未知錯誤"), last);
        }

        /// <summary>序列埠連線：開啟後同樣以 *IDN? 驗證。</summary>
        private void ConnectSerial()
        {
            if (string.IsNullOrEmpty(PortName))
                throw new InvalidOperationException("尚未選擇 COM Port。");

            var t = new SerialTransport(PortName, BaudRate);
            t.Open();
            _transport = t;
            WriteLog(LogKind.Info, "Serial Connect OK  " + PortName + " @ " + BaudRate + " bps");

            VerifyIdentity("Serial");
        }

        /// <summary>連線成功不可只以連線成功判定：必須 *IDN? 有回應才算驗證通過，否則釋放並丟例外。</summary>
        private void VerifyIdentity(string tag)
        {
            string idn;
            try
            {
                idn = CleanIdentifyText(Query("*IDN?"));
            }
            catch (Exception ex)
            {
                DropConnection();
                WriteLog(LogKind.Error, tag + " Connect Failed: *IDN? " + ex.Message);
                throw new InvalidOperationException("*IDN? 查詢失敗：" + ex.Message, ex);
            }

            if (string.IsNullOrEmpty(idn))
            {
                DropConnection();
                WriteLog(LogKind.Error, tag + " Connect Failed: *IDN? 無回應");
                throw new InvalidOperationException("*IDN? 無回應，連線未通過驗證。");
            }

            Idn = idn;
            WriteLog(LogKind.Info, "*IDN? Query OK  " + idn);
            WriteLog(LogKind.Info, tag + " Connect Verified");
        }

        public void Disconnect()
        {
            lock (_ioGate)
            {
                var transport = _transport;
                _transport = null;
                if (transport != null)
                {
                    try { transport.Dispose(); }
                    catch { }
                    WriteLog(LogKind.Info, "電表已中斷連線");
                }
            }
        }

        public void Reset()
        {
            Send("*CLS");
            Send("*RST");
        }

        public void SetMode(MeasurementMode mode)
        {
            _mode = mode;
            Send(mode == MeasurementMode.Resistance ? "CONF:RES" : "CONF:VOLT:DC");
        }

        public void SetRangeAuto()
        {
            Send(_mode == MeasurementMode.Resistance ? "CONF:RES" : "CONF:VOLT:DC");
        }

        public void SetRange(string range)
        {
            if (string.IsNullOrEmpty(range))
            {
                SetRangeAuto();
                return;
            }

            if (_mode == MeasurementMode.DcVoltage)
                WriteLog(LogKind.Info, "Set DC Voltage Range = " + range + "V");

            string conf = _mode == MeasurementMode.Resistance ? "CONF:RES " : "CONF:VOLT:DC ";
            Send(conf + range); // 例: CONF:VOLT:DC 100
        }

        public void SetDcVoltageModeWithRange(double range)
        {
            _mode = MeasurementMode.DcVoltage;
            if (range <= 0)
            {
                // 0 = Auto：送 CONF:VOLT:DC（不帶檔位）
                WriteLog(LogKind.Info, "Set DC Voltage Range = Auto");
                Send("CONF:VOLT:DC");
            }
            else
            {
                string r = range.ToString("0.######", CultureInfo.InvariantCulture);
                WriteLog(LogKind.Info, "Set DC Voltage Range = " + r + "V");
                Send("CONF:VOLT:DC " + r); // 固定檔位，避免後續 auto 蓋掉
            }
        }

        public double Read()
        {
            string resp = Query("READ?");
            string first = resp.Split(',')[0].Trim();

            double value;
            if (!double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new FormatException("無法解析量測值: " + resp);

            return value;
        }

        /// <summary>主動查詢機型識別（實機驗證用）。</summary>
        public string Identify()
        {
            return CleanIdentifyText(Query("*IDN?"));
        }

        /// <summary>
        /// 背景心跳偵測：靜默送出 *IDN? 確認連線是否存活（成功不寫 Log，避免洗版）。
        /// 失敗時立即釋放連線（DropConnection）並回傳 false，呼叫端據此更新 UI 為未連線。
        /// </summary>
        public bool PingDevice()
        {
            lock (_ioGate)
            {
                if (!IsConnected)
                    return false;

                try
                {
                    _transport.WriteLine("*IDN?");
                    _transport.ReadLine();
                    return true;
                }
                catch (Exception ex)
                {
                    WriteLog(LogKind.Error, "心跳偵測失敗，判定斷線: " + ex.Message);
                    MarkLinkLost();   // 保留 socket，待重連時釋放（避免 GDM 舊 session 殘留）
                    return false;
                }
            }
        }

        /// <summary>
        /// 清理 *IDN? 回傳字串：移除 NULL / 控制字元 / 0xFF(ÿ) / 0xFE(þ) / BOM 等不可列印字元，
        /// 僅保留英數字、逗號、句點、空白、底線、連字號，並去除首尾空白與殘留逗號。
        /// 例如 "ÿÿ,GWInstek,GDM8261A,GER865222,1.02" → "GWInstek,GDM8261A,GER865222,1.02"。
        /// </summary>
        public static string CleanIdentifyText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                bool keep =
                    (c >= 'A' && c <= 'Z') ||
                    (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') ||
                    c == ',' || c == '.' || c == ' ' || c == '_' || c == '-';
                if (keep)
                    sb.Append(c);
            }

            // 去除首尾空白，以及被過濾字元（如 "ÿÿ,"）剝除後殘留的開頭/結尾逗號。
            return sb.ToString().Trim().Trim(',', ' ').Trim();
        }

        private void Send(string command)
        {
            lock (_ioGate)
            {
                if (!IsConnected)
                    throw new InvalidOperationException("電表尚未連線。");

                try
                {
                    _transport.WriteLine(command);
                    WriteLog(LogKind.Tx, command);
                }
                catch (Exception ex)
                {
                    WriteLog(LogKind.Error, "TX " + command + " 失敗: " + ex.Message);
                    MarkLinkLost(); // 判定斷線（保留 socket 至重連時釋放）
                    throw;
                }
            }
        }

        private string Query(string command)
        {
            lock (_ioGate)
            {
                if (!IsConnected)
                    throw new InvalidOperationException("電表尚未連線。");

                try
                {
                    _transport.WriteLine(command);
                    WriteLog(LogKind.Tx, command);
                    string resp = _transport.ReadLine();
                    WriteLog(LogKind.Rx, resp);
                    return resp;
                }
                catch (Exception ex)
                {
                    WriteLog(LogKind.Error, "Query " + command + " 失敗: " + ex.Message);
                    MarkLinkLost(); // 判定斷線（保留 socket 至重連時釋放）
                    throw;
                }
            }
        }

        /// <summary>
        /// 通訊失敗時立即釋放底層連線（TcpClient / NetworkStream 或序列埠），
        /// 使 <see cref="IsConnected"/> 立即回報未連線，避免後續沿用失效連線。
        /// </summary>
        private void DropConnection()
        {
            bool had = _transport != null;
            CloseTransport();
            if (had)
            {
                WriteLog(LogKind.Error, "通訊異常，連線已釋放（判定為斷線）");
                // 已連線後的中途斷線才通知 UI；連線建立階段失敗由 Connect 流程處理
                if (!_connecting)
                {
                    var h = ConnectionLost;
                    if (h != null) h(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// 執行中（已連線）通訊失敗：標記斷線並通知 UI，但「不 Dispose」舊 socket，
        /// 留待重連 / 中斷時（線材接回）才釋放，避免 GDM 端舊 session 殘留導致重連被 RST。
        /// </summary>
        private void MarkLinkLost()
        {
            if (_linkLost || _transport == null || _connecting)
                return;
            _linkLost = true;
            WriteLog(LogKind.Error, "通訊異常，判定為斷線（連線物件保留至重連時釋放）");
            var h = ConnectionLost;
            if (h != null) h(this, EventArgs.Empty);
        }

        /// <summary>完整釋放底層連線（NetworkStream/TcpClient 或序列埠）並清為 null，不寫 Log。</summary>
        private void CloseTransport()
        {
            var transport = _transport;
            _transport = null;
            _linkLost = false;
            if (transport != null)
            {
                try { transport.Dispose(); }
                catch { }
            }
        }

        private void WriteLog(LogKind kind, string message)
        {
            if (Log != null)
                Log.Write(kind, message);
        }
    }
}
