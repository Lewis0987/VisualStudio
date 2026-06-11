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
}
