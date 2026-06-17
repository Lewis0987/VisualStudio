using System;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;

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
        private readonly int _connectTimeoutMs;
        private readonly int _ioTimeoutMs;
        private TcpClient _client;
        private NetworkStream _stream;

        public TcpTransport(string host, int port)
            : this(host, port, 3000, 3000)
        {
        }

        public TcpTransport(string host, int port, int connectTimeoutMs, int ioTimeoutMs)
        {
            _host = host;
            _port = port;
            _connectTimeoutMs = connectTimeoutMs > 0 ? connectTimeoutMs : 3000;
            _ioTimeoutMs = ioTimeoutMs > 0 ? ioTimeoutMs : 3000;
        }

        public bool IsOpen => _client != null && _client.Connected;

        public string Description => $"{_host}:{_port}";

        // 連線邏輯完全比照 D:\GDM-8261A-Tester-2：每次都建立全新 TcpClient，
        // 連線前先 Close() 釋放任何殘留物件，不重用舊連線。
        public void Open()
        {
            Close(); // 先清掉任何舊連線，避免殘留 TcpClient / Stream

            _client = new TcpClient();

            var result = _client.BeginConnect(_host, _port, null, null);
            if (!result.AsyncWaitHandle.WaitOne(_connectTimeoutMs))
            {
                Close(); // 逾時：釋放 client，維持乾淨的未連線狀態
                throw new TimeoutException($"GDM LAN 連線逾時 {_host}:{_port}");
            }

            try
            {
                _client.EndConnect(result);
                _stream = _client.GetStream();
                _stream.ReadTimeout = _ioTimeoutMs;
                _stream.WriteTimeout = _ioTimeoutMs;
            }
            catch
            {
                Close(); // 連線中斷 / 拒絕：釋放後再往外丟，由上層顯示錯誤
                throw;
            }
        }

        /// <summary>清空輸入緩衝區的殘留資料（重連後避免讀到上一個 session 的舊回應）。</summary>
        public void DrainInput()
        {
            try
            {
                if (_stream == null)
                    return;
                while (_stream.DataAvailable)
                {
                    if (_stream.ReadByte() < 0)
                        break;
                }
            }
            catch { }
        }

        public void Close()
        {
            // 完整釋放 NetworkStream / TcpClient，避免後續沿用失效的舊連線物件
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }

            try { _stream?.Dispose(); } catch { }
            try { _client?.Dispose(); } catch { }

            _stream = null;
            _client = null;
        }

        public void WriteLine(string command)
        {
            if (_stream == null)
                throw new IOException("LAN 連線已關閉。");
            byte[] data = Encoding.ASCII.GetBytes(command + "\n");
            _stream.Write(data, 0, data.Length);
        }

        public string ReadLine()
        {
            if (_stream == null)
                throw new IOException("LAN 連線已關閉。");

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

}
