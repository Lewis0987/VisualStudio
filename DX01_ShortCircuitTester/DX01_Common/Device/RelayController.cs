using System;

namespace DX01_ShortCircuitTester.Device
{
    /// <summary>
    /// 繼電器控制介面。Relay 代碼為兩位字串：
    /// "00" / "01" / "10" / "11"，對應流程圖各量測路徑。
    /// 目前以 Mock 模擬，之後可實作真實硬體（USB HID / 序列埠）。
    /// </summary>
    public interface IRelayController
    {
        bool IsConnected { get; }
        string CurrentCode { get; }

        void Connect();
        void Disconnect();

        /// <summary>切換繼電器到指定代碼（"00"/"01"/"10"/"11"）。</summary>
        void SetRelay(string code);
    }

    /// <summary>
    /// Relay 模擬實作：僅在記憶體中記錄目前代碼，不操作任何硬體。
    /// </summary>
    public sealed class MockRelayController : IRelayController
    {
        public bool IsConnected { get; private set; }
        public string CurrentCode { get; private set; }

        public MockRelayController()
        {
            CurrentCode = "00";
        }

        public void Connect()
        {
            IsConnected = true;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        public void SetRelay(string code)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Relay 尚未連線。");

            if (code != "00" && code != "01" && code != "10" && code != "11")
                throw new ArgumentException("不支援的 Relay 代碼: " + code, nameof(code));

            CurrentCode = code;
        }
    }
}
