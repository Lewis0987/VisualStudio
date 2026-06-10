using HidSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GDM8261A_Tester
{
    /// <summary>
    /// USBRelay2 (VID/PID 16C0:05DF) 的 HID 控制封裝。
    /// 只負責裝置偵測 / 連線 / 開關繼電器，不含任何 UI。
    /// MainForm 透過此類別呼叫，達到 UI 與硬體邏輯分離。
    /// </summary>
    public class UsbRelayController : IDisposable
    {
        private const int VendorId = 0x16C0;
        private const int ProductId = 0x05DF;
        private const byte ReportId = 0x00;
        private const int ReportSize = 8;
        private const int ReportPacketSize = ReportSize + 1;

        private HidDevice _device;
        private HidStream _stream;

        /// <summary>目前是否已連線。</summary>
        public bool IsConnected
        {
            get { return _stream != null; }
        }

        /// <summary>是否偵測得到 USBRelay2 裝置（不需連線）。</summary>
        public bool DetectDevice()
        {
            return DeviceList.Local.GetHidDevices(VendorId, ProductId).Any();
        }

        /// <summary>連線到第一個 USBRelay2，失敗時丟出例外。</summary>
        public void Connect()
        {
            if (_stream != null)
                return;

            List<HidDevice> devices = DeviceList.Local.GetHidDevices(VendorId, ProductId).ToList();
            if (devices.Count == 0)
                throw new InvalidOperationException("找不到 USBRelay2，請確認 USB 已連接 (VID/PID 16C0:05DF)。");

            HidStream stream;
            _device = devices[0];
            if (!_device.TryOpen(out stream))
                throw new InvalidOperationException("裝置開啟失敗，可能被其他程式佔用或權限不足。");

            stream.ReadTimeout = 500;
            stream.WriteTimeout = 500;
            _stream = stream;
        }

        /// <summary>中斷連線（可重複呼叫）。</summary>
        public void Disconnect()
        {
            if (_stream != null)
            {
                try { _stream.Dispose(); }
                catch { }
            }
            _stream = null;
            _device = null;
        }

        /// <summary>設定指定通道的繼電器開 (true) / 關 (false)，失敗時丟出例外。</summary>
        public void SetRelay(int channel, bool on)
        {
            if (on)
                WriteCommand(0xFF, channel, 0x01);
            else
                WriteCommand(0xFD, channel, 0x00);
        }

        private void WriteCommand(byte command, int channel, byte value)
        {
            if (_stream == null)
                throw new InvalidOperationException("尚未連線到 USBRelay2。");

            byte[] payload = BuildCommand(command, channel, value, ReportPacketSize);
            byte[] legacy = BuildCommand(command, channel, value, ReportSize);
            List<string> errors = new List<string>();

            // 不同韌體 / 驅動對 9-byte / 8-byte、SetFeature / Write 接受度不同，逐一嘗試
            if (TrySend(errors, "SetFeature 9", () => _stream.SetFeature(payload))) return;
            if (TrySend(errors, "Write 9", () => _stream.Write(payload))) return;
            if (TrySend(errors, "SetFeature 8", () => _stream.SetFeature(legacy))) return;
            if (TrySend(errors, "Write 8", () => _stream.Write(legacy))) return;

            throw new InvalidOperationException("HID 寫入失敗: " + string.Join(" | ", errors));
        }

        private static bool TrySend(List<string> errors, string label, Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                errors.Add(label + ": " + ex.Message);
                return false;
            }
        }

        private static byte[] BuildCommand(byte command, int channel, byte value, int size)
        {
            byte[] packet = new byte[size];
            packet[0] = ReportId;
            packet[1] = command;
            packet[2] = (byte)channel;
            packet[3] = value;
            return packet;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
