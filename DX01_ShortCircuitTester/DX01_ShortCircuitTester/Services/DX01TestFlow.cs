using System;
using System.Threading;
using System.Threading.Tasks;
using DX01_ShortCircuitTester.Device;
using DX01_ShortCircuitTester.Models;

namespace DX01_ShortCircuitTester.Services
{
    /// <summary>步驟開始事件參數。</summary>
    public sealed class StepStartedEventArgs : EventArgs
    {
        public int StepNumber { get; }
        public string Description { get; }
        public StepStartedEventArgs(int step, string description)
        {
            StepNumber = step;
            Description = description;
        }
    }

    /// <summary>量測更新事件參數。</summary>
    public sealed class MeasurementEventArgs : EventArgs
    {
        public double Value { get; }
        public string Unit { get; }
        public MeasurementEventArgs(double value, string unit)
        {
            Value = value;
            Unit = unit;
        }
    }

    /// <summary>
    /// DX01 外殼短路測試流程（共 10 步驟）。
    /// 與 UI 解耦：透過事件回報進度，呼叫端（MainForm）只負責顯示。
    /// 判定值與等待時間皆讀取 <see cref="AppSettings.Current"/>（由參數設定視窗 / Config 控制）。
    /// </summary>
    public sealed class DX01TestFlow
    {
        private readonly IRelayController _relay;
        private readonly IGdm8261AController _meter;

        public event EventHandler<StepStartedEventArgs> StepStarted;
        public event EventHandler<string> RelayChanged;
        public event EventHandler<MeasurementEventArgs> Measured;
        public event EventHandler<TestStepResult> StepCompleted;
        public event EventHandler<string> StatusChanged;

        public DX01TestFlow(IRelayController relay, IGdm8261AController meter)
        {
            _relay = relay;
            _meter = meter;
        }

        private static AppSettings Cfg { get { return AppSettings.Current; } }

        /// <summary>
        /// 執行整個測試流程。任一步驟不合格即停止並判定 NG。
        /// 在 UI 執行緒上 await 呼叫時，所有事件都會在 UI 執行緒觸發，可直接更新畫面。
        /// </summary>
        public async Task<TestResult> RunAsync(string serialNumber, CancellationToken token)
        {
            var result = new TestResult
            {
                SerialNumber = serialNumber,
                StartTime = DateTime.Now
            };

            RaiseStatus("測試中…");

            try
            {
                // Step 1 初始化電表：Relay=00、電阻模式、Auto 檔位
                RaiseStep(1, "初始化電表 (Relay=00, 電阻模式, Auto)");
                _meter.Reset();
                SwitchRelay("00");
                _meter.SetMode(MeasurementMode.Resistance);
                _meter.SetRangeAuto();
                await Delay(Cfg.WaitMs(1), token);
                AddInfoStep(result, 1, "初始化電表", "00", "電阻");

                // Step 2 掃描 Label，記錄序號（序號已由 UI 傳入）
                RaiseStep(2, "掃描 Label，序號 = " + serialNumber);
                await Delay(Cfg.WaitMs(2), token);
                AddInfoStep(result, 2, "掃描 Label / 記錄序號", "-", "-");

                // Step 3 外殼對機殼導通：Relay=00，R < 10Ω
                if (!await MeasureStep(result, 3, "外殼對機殼導通", "00",
                        MeasurementMode.Resistance, "Ω", null, Cfg.Step3CaseToChassisMax, token))
                    return Finish(result);

                // Step 4 P+ 對外殼絕緣：Relay=01，R > 1MΩ
                if (!await MeasureStep(result, 4, "P+ 對外殼絕緣", "01",
                        MeasurementMode.Resistance, "Ω", Cfg.Step4PPlusInsulationMin, null, token))
                    return Finish(result);

                // Step 5 P- 對外殼絕緣：Relay=10，R > 1MΩ
                if (!await MeasureStep(result, 5, "P- 對外殼絕緣", "10",
                        MeasurementMode.Resistance, "Ω", Cfg.Step5PMinusInsulationMin, null, token))
                    return Finish(result);

                // Step 6 切換電壓量測：DC Voltage、Range=100V
                RaiseStep(6, "切換電壓量測 (DC Voltage, Range=100V)");
                _meter.SetMode(MeasurementMode.DcVoltage);
                _meter.SetRange("100");
                await Delay(Cfg.WaitMs(6), token);

                // Step 7 電壓總值：Relay=11，V > 45V（Step7WaitMs 通常較長，待電壓穩定）
                if (!await MeasureStep(result, 7, "電壓總值", "11",
                        MeasurementMode.DcVoltage, "V", Cfg.Step7TotalVoltageMin, null, token))
                    return Finish(result);

                // Step 8 P+ / P- 電壓：Relay=11，48V ~ 51V
                if (!await MeasureStep(result, 8, "P+ / P- 電壓", "11",
                        MeasurementMode.DcVoltage, "V", Cfg.Step8PPlusMinusMin, Cfg.Step8PPlusMinusMax, token))
                    return Finish(result);

                // Step 9 P+ 對外殼電壓：Relay=01，V < 1V
                if (!await MeasureStep(result, 9, "P+ 對外殼電壓", "01",
                        MeasurementMode.DcVoltage, "V", null, Cfg.Step9PPlusToCaseMax, token))
                    return Finish(result);

                // Step 10 P- 對外殼電壓：Relay=10，V < 1V
                if (!await MeasureStep(result, 10, "P- 對外殼電壓", "10",
                        MeasurementMode.DcVoltage, "V", null, Cfg.Step10PMinusToCaseMax, token))
                    return Finish(result);

                result.Completed = true;
            }
            catch (OperationCanceledException)
            {
                result.Aborted = true;
            }

            return Finish(result);
        }

        /// <summary>記錄一個資訊步驟（無量測值），寫入結果並通知 UI。</summary>
        private void AddInfoStep(TestResult result, int step, string name, string relayCode, string mode)
        {
            var stepResult = new TestStepResult
            {
                StepNumber = step,
                StepName = name,
                RelayCode = relayCode,
                Mode = mode,
                IsInfo = true,
                Pass = true,
                Time = DateTime.Now
            };
            result.Steps.Add(stepResult);
            StepCompleted?.Invoke(this, stepResult);
        }

        /// <summary>執行一個量測步驟並判定。回傳是否通過。等待時間依步驟由 AppSettings 控制。</summary>
        private async Task<bool> MeasureStep(
            TestResult result, int step, string name, string relayCode,
            MeasurementMode mode, string unit, double? low, double? high,
            CancellationToken token)
        {
            RaiseStep(step, name + " (Relay=" + relayCode + ")");

            SwitchRelay(relayCode);
            await Delay(Cfg.RelaySwitchDelayMs, token); // 繼電器切換穩定時間
            _meter.SetMode(mode);
            await Delay(Cfg.WaitMs(step), token);       // 該步驟量測等待

            double value = _meter.Read();
            Measured?.Invoke(this, new MeasurementEventArgs(value, unit));

            bool pass = Evaluate(value, low, high);

            var stepResult = new TestStepResult
            {
                StepNumber = step,
                StepName = name,
                RelayCode = relayCode,
                Mode = mode == MeasurementMode.Resistance ? "電阻" : "DC電壓",
                Value = value,
                Unit = unit,
                LowLimit = low,
                HighLimit = high,
                Pass = pass,
                Time = DateTime.Now
            };

            result.Steps.Add(stepResult);
            StepCompleted?.Invoke(this, stepResult);

            return pass;
        }

        /// <summary>依上下限判定：兩者皆有=區間；只有上限=小於；只有下限=大於。</summary>
        private static bool Evaluate(double value, double? low, double? high)
        {
            if (low.HasValue && high.HasValue)
                return value >= low.Value && value <= high.Value;
            if (high.HasValue)
                return value < high.Value;
            if (low.HasValue)
                return value > low.Value;
            return true;
        }

        private void SwitchRelay(string code)
        {
            _relay.SetRelay(code);
            RelayChanged?.Invoke(this, code);
        }

        private void RaiseStep(int step, string description)
        {
            StepStarted?.Invoke(this, new StepStartedEventArgs(step, description));
        }

        private void RaiseStatus(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        private async Task Delay(int ms, CancellationToken token)
        {
            if (ms > 0)
                await Task.Delay(ms, token);
            else
                token.ThrowIfCancellationRequested();
        }

        private TestResult Finish(TestResult result)
        {
            result.EndTime = DateTime.Now;
            RaiseStatus(result.Aborted ? "已中止" : (result.IsPass ? "測試完成：OK" : "測試完成：NG"));
            return result;
        }
    }
}
