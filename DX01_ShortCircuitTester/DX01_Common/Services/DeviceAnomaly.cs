using System;
using System.IO;
using System.Net.Sockets;

namespace DX01_ShortCircuitTester.Services
{
    /// <summary>
    /// 設備 / 網路異常分類器。
    /// 將底層通訊例外（TCP / Serial / HID Relay / READ）對應為固定的異常類型字串，
    /// 供 UI 顯示「設備異常」Popup 與 CSV 的 ErrorType 欄位使用。
    /// </summary>
    public static class DeviceAnomaly
    {
        // 對外公開的異常類型常數（與規格一致）
        public const string LanTimeout = "LAN Timeout";
        public const string SocketError = "Socket Exception";
        public const string SerialError = "Serial Exception";
        public const string RelayError = "Relay Exception";
        public const string SetFeatureFailed = "SetFeature Failed";
        public const string WriteFailed = "Write Failed";
        public const string ReadTimeout = "READ Timeout";
        public const string DeviceNotFound = "Device Not Found";
        public const string ConnectFailed = "Connect Failed";

        /// <summary>
        /// 將例外分類為固定的設備異常類型字串。
        /// isLan：目前連線是否為 LAN(TCP)，用於區分逾時 / IO 來源。
        /// </summary>
        public static string Classify(Exception ex, bool isLan)
        {
            if (ex == null)
                return "Unknown";

            string msg = ex.Message ?? "";

            // Socket（LAN 底層）
            if (ex is SocketException)
                return SocketError;

            // 逾時：連線逾時 vs READ 逾時
            if (ex is TimeoutException)
            {
                if (msg.IndexOf("連線", StringComparison.Ordinal) >= 0 ||
                    msg.IndexOf("逾時", StringComparison.Ordinal) >= 0)
                    return isLan ? LanTimeout : SerialError;
                return ReadTimeout;
            }

            // 序列埠存取被拒 / 佔用
            if (ex is UnauthorizedAccessException)
                return SerialError;

            // IO：LAN 視為 Socket，Serial 視為 Serial
            if (ex is IOException)
                return isLan ? SocketError : SerialError;

            // Relay (HID) 與電表連線狀態類例外
            if (ex is InvalidOperationException)
            {
                if (Contains(msg, "找不到 Relay") || Contains(msg, "找不到"))
                    return DeviceNotFound;
                if (Contains(msg, "HID 寫入失敗") || Contains(msg, "SetFeature"))
                    return SetFeatureFailed;
                if (Contains(msg, "開啟失敗"))
                    return ConnectFailed;
                if (Contains(msg, "Relay"))
                    return RelayError;
                if (Contains(msg, "尚未連線"))
                    return ConnectFailed;
                return ConnectFailed;
            }

            // READ? 回傳無法解析
            if (ex is FormatException)
                return ReadTimeout;

            // 其他：回傳例外型別名稱
            return ex.GetType().Name;
        }

        /// <summary>是否為「Relay 相關」異常（用於異常後將 Relay 狀態改為未連線）。</summary>
        public static bool IsRelayRelated(string errorType)
        {
            return errorType == SetFeatureFailed ||
                   errorType == WriteFailed ||
                   errorType == RelayError ||
                   errorType == DeviceNotFound;
        }

        private static bool Contains(string text, string token)
        {
            return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
