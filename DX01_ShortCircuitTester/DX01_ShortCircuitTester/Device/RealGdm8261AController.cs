using System;
using System.Globalization;
using DX01_ShortCircuitTester.Services;

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

        /// <summary>連線後讀回的 *IDN? 機型識別字串。</summary>
        public string Idn { get; private set; }

        /// <summary>除錯日誌（可為 null）。</summary>
        public DebugLog Log { get; set; }

        public bool IsConnected
        {
            get { return _transport != null && _transport.IsOpen; }
        }

        public void Connect()
        {
            if (IsConnected)
                return;

            if (string.IsNullOrEmpty(PortName))
                throw new InvalidOperationException("尚未選擇 COM Port。");

            var transport = new SerialTransport(PortName, BaudRate);
            transport.Open();
            _transport = transport;
            WriteLog(LogKind.Info, "電表連線 " + PortName + " @ " + BaudRate + " bps");

            try
            {
                Idn = Query("*IDN?");
            }
            catch
            {
                Idn = "";
            }
        }

        public void Disconnect()
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

            string conf = _mode == MeasurementMode.Resistance ? "CONF:RES " : "CONF:VOLT:DC ";
            Send(conf + range);
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
            return Query("*IDN?");
        }

        private void Send(string command)
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
                throw;
            }
        }

        private string Query(string command)
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
                throw;
            }
        }

        private void WriteLog(LogKind kind, string message)
        {
            if (Log != null)
                Log.Write(kind, message);
        }
    }
}
