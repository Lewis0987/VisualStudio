using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
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
    /// 判定值與每步驟等待時間皆由 Config\DX01Config.json 載入（找不到則用內建預設）。
    /// </summary>
    public sealed class DX01TestFlow
    {
        private readonly IRelayController _relay;
        private readonly IGdm8261AController _meter;
        private readonly DX01Limits _limits;

        public event EventHandler<StepStartedEventArgs> StepStarted;
        public event EventHandler<string> RelayChanged;
        public event EventHandler<MeasurementEventArgs> Measured;
        public event EventHandler<TestStepResult> StepCompleted;
        public event EventHandler<string> StatusChanged;

        public DX01TestFlow(IRelayController relay, IGdm8261AController meter)
        {
            _relay = relay;
            _meter = meter;
            _limits = DX01Limits.Load();
        }

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
                await Delay(_limits.WaitMs(1), token);
                AddInfoStep(result, 1, "初始化電表", "00", "電阻");

                // Step 2 掃描 Label，記錄序號（序號已由 UI 傳入）
                RaiseStep(2, "掃描 Label，序號 = " + serialNumber);
                await Delay(_limits.WaitMs(2), token);
                AddInfoStep(result, 2, "掃描 Label / 記錄序號", "-", "-");

                // Step 3 外殼對機殼導通：Relay=00，R < 10Ω
                if (!await MeasureStep(result, 3, "外殼對機殼導通", "00",
                        MeasurementMode.Resistance, "Ω", null, _limits.Step3CaseToChassisMax, token))
                    return Finish(result);

                // Step 4 P+ 對外殼絕緣：Relay=01，R > 1MΩ
                if (!await MeasureStep(result, 4, "P+ 對外殼絕緣", "01",
                        MeasurementMode.Resistance, "Ω", _limits.Step4PPlusInsulationMin, null, token))
                    return Finish(result);

                // Step 5 P- 對外殼絕緣：Relay=10，R > 1MΩ
                if (!await MeasureStep(result, 5, "P- 對外殼絕緣", "10",
                        MeasurementMode.Resistance, "Ω", _limits.Step5PMinusInsulationMin, null, token))
                    return Finish(result);

                // Step 6 切換電壓量測：DC Voltage、Range=100V
                RaiseStep(6, "切換電壓量測 (DC Voltage, Range=100V)");
                _meter.SetMode(MeasurementMode.DcVoltage);
                _meter.SetRange("100");
                await Delay(_limits.WaitMs(6), token);

                // Step 7 電壓總值：Relay=11，V > 45V（等待時間較長，待電壓穩定後再讀）
                if (!await MeasureStep(result, 7, "電壓總值", "11",
                        MeasurementMode.DcVoltage, "V", _limits.Step7TotalVoltageMin, null, token))
                    return Finish(result);

                // Step 8 P+ / P- 電壓：Relay=11，48V ~ 51V
                if (!await MeasureStep(result, 8, "P+ / P- 電壓", "11",
                        MeasurementMode.DcVoltage, "V", _limits.Step8PPlusMinusMin, _limits.Step8PPlusMinusMax, token))
                    return Finish(result);

                // Step 9 P+ 對外殼電壓：Relay=01，V < 1V
                if (!await MeasureStep(result, 9, "P+ 對外殼電壓", "01",
                        MeasurementMode.DcVoltage, "V", null, _limits.Step9PPlusToCaseMax, token))
                    return Finish(result);

                // Step 10 P- 對外殼電壓：Relay=10，V < 1V
                if (!await MeasureStep(result, 10, "P- 對外殼電壓", "10",
                        MeasurementMode.DcVoltage, "V", null, _limits.Step10PMinusToCaseMax, token))
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

        /// <summary>執行一個量測步驟並判定。回傳是否通過。等待時間依步驟由 Config 控制。</summary>
        private async Task<bool> MeasureStep(
            TestResult result, int step, string name, string relayCode,
            MeasurementMode mode, string unit, double? low, double? high,
            CancellationToken token)
        {
            RaiseStep(step, name + " (Relay=" + relayCode + ")");

            SwitchRelay(relayCode);
            _meter.SetMode(mode);
            await Delay(_limits.WaitMs(step), token);

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

        /// <summary>
        /// 各步驟判定值與等待時間。預設值即原本寫死的條件；Load() 會嘗試從 Config\DX01Config.json 覆寫。
        /// （net48 無內建 JSON 函式庫且不變更 csproj，故以極簡解析讀取數值欄位；
        ///  任何讀取/解析失敗都回退預設值，不影響測試流程。）
        /// </summary>
        private sealed class DX01Limits
        {
            public double Step3CaseToChassisMax = 10.0;       // 外殼對機殼導通：R < 10Ω
            public double Step4PPlusInsulationMin = 1.0e6;    // P+ 對外殼絕緣：R > 1MΩ
            public double Step5PMinusInsulationMin = 1.0e6;   // P- 對外殼絕緣：R > 1MΩ
            public double Step7TotalVoltageMin = 45.0;        // 總電壓：V > 45V
            public double Step8PPlusMinusMin = 48.0;          // P+/P- 電壓：48V ~
            public double Step8PPlusMinusMax = 51.0;          // P+/P- 電壓：~ 51V
            public double Step9PPlusToCaseMax = 1.0;          // P+ 對外殼電壓：V < 1V
            public double Step10PMinusToCaseMax = 1.0;        // P- 對外殼電壓：V < 1V

            public int StepDelayMs = 350;                     // 預設等待（未指定步驟時）
            // 各步驟等待時間（index 1..10）；Step7 預設較長（電壓穩定）
            public int[] StepWaitMs = { 0, 350, 350, 350, 350, 350, 350, 10000, 350, 350, 350 };

            public int WaitMs(int step)
            {
                return (step >= 1 && step <= 10) ? StepWaitMs[step] : StepDelayMs;
            }

            public static DX01Limits Load()
            {
                var cfg = new DX01Limits();
                string path = FindConfigFile();
                if (path == null)
                    return cfg;

                try
                {
                    string json = File.ReadAllText(path);

                    cfg.Step3CaseToChassisMax = ReadNum(json, "step3_caseToChassis_max", cfg.Step3CaseToChassisMax);
                    cfg.Step4PPlusInsulationMin = ReadNum(json, "step4_pPlusInsulation_min", cfg.Step4PPlusInsulationMin);
                    cfg.Step5PMinusInsulationMin = ReadNum(json, "step5_pMinusInsulation_min", cfg.Step5PMinusInsulationMin);
                    cfg.Step7TotalVoltageMin = ReadNum(json, "step7_totalVoltage_min", cfg.Step7TotalVoltageMin);
                    cfg.Step8PPlusMinusMin = ReadNum(json, "step8_pPlusMinusVoltage_min", cfg.Step8PPlusMinusMin);
                    cfg.Step8PPlusMinusMax = ReadNum(json, "step8_pPlusMinusVoltage_max", cfg.Step8PPlusMinusMax);
                    cfg.Step9PPlusToCaseMax = ReadNum(json, "step9_pPlusToCaseVoltage_max", cfg.Step9PPlusToCaseMax);
                    cfg.Step10PMinusToCaseMax = ReadNum(json, "step10_pMinusToCaseVoltage_max", cfg.Step10PMinusToCaseMax);

                    cfg.StepDelayMs = (int)ReadNum(json, "stepDelayMs", cfg.StepDelayMs);
                    for (int s = 1; s <= 10; s++)
                        cfg.StepWaitMs[s] = (int)ReadNum(json, "Step" + s + "WaitMs", cfg.StepWaitMs[s]);
                }
                catch
                {
                    return new DX01Limits();
                }

                return cfg;
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
                if (m.Success && double.TryParse(m.Groups[1].Value,
                        NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    return v;
                return def;
            }
        }
    }
}
