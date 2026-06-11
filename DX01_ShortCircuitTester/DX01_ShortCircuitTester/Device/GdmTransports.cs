using System;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace DX01_ShortCircuitTester.Device
{
    // 直接重用 D:\VisualStudio\GDM8261A_Tester\GDM8261A_Tester\GdmTransports.cs
    // （僅調整命名空間到 DX01_ShortCircuitTester.Device）

    /// <summary>
    /// GDM-8261A 通訊介面抽象層。
    /// USB / RS-232 使用 SerialTransport，LAN 使用 TcpTransport。
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

    /// <summary>
    /// USB 虛擬 COM / RS-232 序列埠通訊
    /// </summary>
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
                DtrEnable = true,
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
            if (_port.IsOpen)
            {
                _port.Close();
            }
        }

        public void WriteLine(string command)
        {
            _port.WriteLine(command);
        }

        public string ReadLine()
        {
            return _port.ReadLine().TrimEnd('\r');
        }

        public void Dispose()
        {
            Close();
            _port.Dispose();
        }
    }

    /// <summary>
    /// LAN TCP 通訊，GDM-8261A 預設 Port 3000
    /// </summary>
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
            var sb = new StringBuilder(64);

            while (true)
            {
                int b = _stream.ReadByte();

                if (b < 0)
                {
                    throw new IOException("連線已被遠端關閉");
                }

                if (b == '\n')
                {
                    break;
                }

                if (b != '\r')
                {
                    sb.Append((char)b);
                }
            }

            return sb.ToString();
        }

        public void Dispose()
        {
            Close();
        }
    }

    /// <summary>GDM 連線方式。</summary>
    public enum GdmConnectionMode
    {
        Serial,
        Lan
    }

    /// <summary>
    /// 從 Config\DX01Config.json 載入 GDM 連線預設值（找不到欄位 / 檔案則用內建預設）。
    /// net48 無內建 JSON 函式庫且不變更 csproj，故以極簡解析讀取欄位。
    /// </summary>
    public static class GdmConnectionConfig
    {
        public static GdmConnectionMode Mode = GdmConnectionMode.Serial;
        public static int Baud = 115200;
        public static string Ip = "192.168.100.100";
        public static int TcpPort = 23;

        public static void Load()
        {
            string path = FindConfigFile();
            if (path == null)
                return;

            try
            {
                string json = File.ReadAllText(path);

                string mode = ReadStr(json, "gdm_connectionMode");
                if (string.Equals(mode, "Lan", StringComparison.OrdinalIgnoreCase))
                    Mode = GdmConnectionMode.Lan;
                else if (string.Equals(mode, "Serial", StringComparison.OrdinalIgnoreCase))
                    Mode = GdmConnectionMode.Serial;

                Baud = (int)ReadNum(json, "gdm_comBaud", Baud);

                string ip = ReadStr(json, "gdm_ip");
                if (!string.IsNullOrEmpty(ip))
                    Ip = ip;

                TcpPort = (int)ReadNum(json, "gdm_tcpPort", TcpPort);
            }
            catch
            {
                // 解析失敗 → 維持預設值
            }
        }

        private static string FindConfigFile()
        {
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                for (int i = 0; i < 7 && !string.IsNullOrEmpty(dir); i++)
                {
                    string c1 = Path.Combine(dir, "Config", "DX01Config.json");
                    if (File.Exists(c1)) return c1;

                    string c2 = Path.Combine(dir, "DX01_ShortCircuitTester", "Config", "DX01Config.json");
                    if (File.Exists(c2)) return c2;

                    dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
                }
            }
            catch { }
            return null;
        }

        private static double ReadNum(string json, string key, double def)
        {
            Match m = Regex.Match(json,
                "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?(?:[eE][-+]?[0-9]+)?)");
            double v;
            if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;
            return def;
        }

        private static string ReadStr(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}
