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

        // 不可只依賴 TcpClient.Connected（拔線後仍可能為 true）。
        // 以非阻塞 Poll 偵測對方是否已關閉（收到 FIN/RST 時可讀但無資料）。
        public bool IsOpen
        {
            get
            {
                if (_client == null || _stream == null)
                    return false;
                try
                {
                    if (!_client.Connected)
                        return false;
                    var sock = _client.Client;
                    if (sock.Poll(0, SelectMode.SelectRead) && sock.Available == 0)
                        return false; // 對方已關閉
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public string Description => $"{_host}:{_port}";

        public void Open()
        {
            _client = new TcpClient();
            _client.NoDelay = true;
            _client.ReceiveTimeout = _ioTimeoutMs;
            _client.SendTimeout = _ioTimeoutMs;

            var result = _client.BeginConnect(_host, _port, null, null);
            if (!result.AsyncWaitHandle.WaitOne(_connectTimeoutMs))
            {
                try { _client.Close(); } catch { }
                throw new TimeoutException($"GDM LAN 連線逾時 {_host}:{_port}");
            }

            _client.EndConnect(result);

            // 中斷時送出 RST 立即收回連線，讓單一連線的儀器（GDM）能盡快釋放舊 session 供重連。
            try { _client.LingerState = new LingerOption(true, 0); }
            catch { }

            // 開啟 TCP KeepAlive（額外保護）：讓 OS 在 ~5 秒內偵測到死連線。
            try { EnableKeepAlive(_client.Client, 5000, 1000); }
            catch { }

            _stream = _client.GetStream();
            _stream.ReadTimeout = _ioTimeoutMs;
            _stream.WriteTimeout = _ioTimeoutMs;
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

        /// <summary>開啟並設定 TCP KeepAlive（Windows：透過 IOControl 設定 tcp_keepalive 結構）。</summary>
        private static void EnableKeepAlive(Socket socket, uint keepAliveTimeMs, uint keepAliveIntervalMs)
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            // struct tcp_keepalive { u_long onoff; u_long keepalivetime; u_long keepaliveinterval; }
            byte[] inValue = new byte[12];
            System.BitConverter.GetBytes((uint)1).CopyTo(inValue, 0);
            System.BitConverter.GetBytes(keepAliveTimeMs).CopyTo(inValue, 4);
            System.BitConverter.GetBytes(keepAliveIntervalMs).CopyTo(inValue, 8);
            socket.IOControl(IOControlCode.KeepAliveValues, inValue, null);
        }

        public void Close()
        {
            // 完整釋放，避免後續沿用失效的 TcpClient / NetworkStream
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
