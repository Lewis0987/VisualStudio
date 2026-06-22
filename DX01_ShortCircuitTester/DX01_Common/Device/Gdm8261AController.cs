using System;

namespace DX01_ShortCircuitTester.Device
{
    /// <summary>量測模式。</summary>
    public enum MeasurementMode
    {
        Resistance,
        DcVoltage
    }

    /// <summary>
    /// GDM-8261A 數位電表控制介面。
    /// 目前以 Mock 模擬，之後可實作真實硬體（LAN / RS-232 SCPI 指令）。
    /// </summary>
    public interface IGdm8261AController
    {
        bool IsConnected { get; }

        void Connect();
        void Disconnect();

        /// <summary>初始化 / 重置電表 (*RST 等)。</summary>
        void Reset();

        void SetMode(MeasurementMode mode);
        void SetRangeAuto();
        void SetRange(string range);

        /// <summary>切到 DC 電壓模式並固定檔位（送出 CONF:VOLT:DC &lt;range&gt;），避免被 auto 蓋掉。</summary>
        void SetDcVoltageModeWithRange(double range);

        /// <summary>讀取目前量測值（電阻回傳 Ω，電壓回傳 V）。</summary>
        double Read();

        /// <summary>
        /// 靜默讀取量測值：仍送出 READ? 取得電壓，但「不」將 TX/RX 寫入 Debug Log。
        /// 供等待 Power ON/OFF 的高頻輪詢使用，避免 Debug Log 被 TX/RX 洗版。
        /// </summary>
        double ReadQuiet();
    }

    /// <summary>
    /// GDM-8261A 模擬實作。
    /// 依「目前量測模式 + 目前 Relay 路徑」回傳符合流程判定的模擬值，
    /// 讓整個測試流程在無硬體時即可完整跑 PASS。
    /// RandomNg = true 時，會隨機讓某次讀值落在不合格範圍，方便驗證 NG 流程。
    /// </summary>
    public sealed class MockGdm8261AController : IGdm8261AController
    {
        private readonly IRelayController _relay;
        private readonly Random _rng = new Random();
        private MeasurementMode _mode = MeasurementMode.Resistance;

        /// <summary>是否隨機注入 NG（模擬不良品）。</summary>
        public bool RandomNg { get; set; }

        public bool IsConnected { get; private set; }

        /// <summary>
        /// 模擬電表需要知道目前 Relay 路徑才能回傳對應的物理量測值，
        /// 因此注入 Relay 控制器參考（真實電表實作則不需要）。
        /// </summary>
        public MockGdm8261AController(IRelayController relay)
        {
            _relay = relay;
        }

        public void Connect()
        {
            IsConnected = true;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        public void Reset()
        {
            _mode = MeasurementMode.Resistance;
        }

        public void SetMode(MeasurementMode mode)
        {
            _mode = mode;
        }

        public void SetRangeAuto()
        {
            // 模擬：無動作
        }

        public void SetRange(string range)
        {
            // 模擬：無動作
        }

        public void SetDcVoltageModeWithRange(double range)
        {
            _mode = MeasurementMode.DcVoltage;
        }

        public double Read()
        {
            if (!IsConnected)
                throw new InvalidOperationException("電表尚未連線。");

            bool fault = RandomNg && _rng.NextDouble() < 0.18;
            return Simulate(fault);
        }

        /// <summary>模擬：靜默讀取與一般讀取相同（Mock 本就不寫 Debug Log）。</summary>
        public double ReadQuiet()
        {
            return Read();
        }

        /// <summary>依模式與 Relay 代碼模擬量測值；fault=true 時回傳不合格值。</summary>
        private double Simulate(bool fault)
        {
            string code = _relay != null ? _relay.CurrentCode : "00";

            if (_mode == MeasurementMode.Resistance)
            {
                if (code == "00")
                {
                    // 外殼對機殼導通：合格 < 10Ω
                    return fault ? 5.0e6 : 2.0 + _rng.NextDouble() * 4.0;     // 2 ~ 6 Ω
                }

                // P+/P- 對外殼絕緣：合格 > 1MΩ
                return fault ? 50.0 : 4.5e6 + _rng.NextDouble() * 2.0e6;       // 4.5M ~ 6.5M Ω
            }

            // DC Voltage
            if (code == "11")
            {
                // 電壓總值 (>45V) 及 P+/P- (48~51V)
                return fault ? 30.0 : 49.0 + _rng.NextDouble() * 1.5;          // 49.0 ~ 50.5 V
            }

            // P+/P- 對外殼電壓：合格 < 1V
            return fault ? 5.0 : _rng.NextDouble() * 0.4;                      // 0 ~ 0.4 V
        }
    }
}
