using HidSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using DX01_ShortCircuitTester.Services; // VID/PID 取自 AppSettings.Current

namespace DX01_ShortCircuitTester.Device
{
    /// <summary>
    /// 真實 Relay Board 控制器（USBRelay2，USB HID）。
    /// HID 寫入邏輯重用 D:\UsbRelayWinForms\MainForm.cs：
    /// VID/PID 16C0:05DF、9/8-byte + SetFeature/Write 逐一嘗試、0xFF 開 / 0xFD 關。
    /// 每次切換 / 錯誤都寫入 <see cref="DebugLog"/>。
    ///
    /// Relay 代碼對應（左字元 = Relay1，右字元 = Relay2）：
    ///   "00" 兩路皆關 / "01" Relay2 開 / "10" Relay1 開 / "11" 兩路皆開
    /// 實際接線若相反，調整 SetRelay 內的對應即可。
    /// </summary>
    public sealed class RealRelayController : IRelayController
    {
        private const byte ReportId = 0x00;
        private const int ReportSize = 8;
        private const int ReportPacketSize = ReportSize + 1;

        private HidDevice _device;
        private HidStream _stream;

        /// <summary>除錯日誌（可為 null）。</summary>
        public DebugLog Log { get; set; }

        public bool IsConnected
        {
            get { return _stream != null; }
        }

        public string CurrentCode { get; private set; }

        public RealRelayController()
        {
            CurrentCode = "00";
        }

        /// <summary>是否偵測得到 Relay Board（不需連線）。</summary>
        public bool DetectDevice()
        {
            return DeviceList.Local.GetHidDevices(AppSettings.Current.VendorId, AppSettings.Current.ProductId).Any();
        }

        public void Connect()
        {
            if (_stream != null)
                return;

            List<HidDevice> devices = DeviceList.Local.GetHidDevices(AppSettings.Current.VendorId, AppSettings.Current.ProductId).ToList();
            if (devices.Count == 0)
                throw new InvalidOperationException("找不到 Relay Board (USB VID/PID " +
                    AppSettings.Current.VendorIdHex + ":" + AppSettings.Current.ProductIdHex + ")。");

            HidStream stream;
            _device = devices[0];
            if (!_device.TryOpen(out stream))
                throw new InvalidOperationException("Relay 開啟失敗，可能被其他程式佔用或權限不足。");

            stream.ReadTimeout = 500;
            stream.WriteTimeout = 500;
            _stream = stream;
            WriteLog(LogKind.Info, "Relay 連線成功");

            // 連線後復位為全關
            SetRelay("00");
        }

        public void Disconnect()
        {
            var stream = _stream;
            _stream = null;
            _device = null;
            if (stream != null)
            {
                try { stream.Dispose(); }
                catch { }
                WriteLog(LogKind.Info, "Relay 已中斷連線");
            }
        }

        public void SetRelay(string code)
        {
            if (_stream == null)
                throw new InvalidOperationException("Relay 尚未連線。");

            if (code != "00" && code != "01" && code != "10" && code != "11")
                throw new ArgumentException("不支援的 Relay 代碼: " + code, nameof(code));

            SetChannel(1, code[0] == '1'); // 左字元 → Relay1
            SetChannel(2, code[1] == '1'); // 右字元 → Relay2

            CurrentCode = code;
            WriteLog(LogKind.Relay, "Relay " + code);
        }

        private void SetChannel(int channel, bool on)
        {
            if (on)
                WriteCommand(0xFF, channel, 0x01);
            else
                WriteCommand(0xFD, channel, 0x00);
        }

        private void WriteCommand(byte command, int channel, byte value)
        {
            byte[] payload = Build(command, channel, value, ReportPacketSize);
            byte[] legacy = Build(command, channel, value, ReportSize);
            var errors = new List<string>();

            if (TrySend(errors, () => _stream.SetFeature(payload))) return;
            if (TrySend(errors, () => _stream.Write(payload))) return;
            if (TrySend(errors, () => _stream.SetFeature(legacy))) return;
            if (TrySend(errors, () => _stream.Write(legacy))) return;

            string detail = "HID 寫入失敗 (ch" + channel + "): " + string.Join(" | ", errors);
            WriteLog(LogKind.Error, detail);
            throw new InvalidOperationException(detail);
        }

        private static bool TrySend(List<string> errors, Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
                return false;
            }
        }

        private static byte[] Build(byte command, int channel, byte value, int size)
        {
            var packet = new byte[size];
            packet[0] = ReportId;
            packet[1] = command;
            packet[2] = (byte)channel;
            packet[3] = value;
            return packet;
        }

        private void WriteLog(LogKind kind, string message)
        {
            if (Log != null)
                Log.Write(kind, message);
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
