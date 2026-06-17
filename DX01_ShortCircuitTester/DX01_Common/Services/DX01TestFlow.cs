using System;
using System.Collections.Generic;
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
        public string Range { get; }
        public StepStartedEventArgs(int step, string description, string range)
        {
            StepNumber = step;
            Description = description;
            Range = range;
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

    /// <summary>產品 Power ON 檢查結果（測試開始前執行，不建立 TestResult）。</summary>
    public sealed class PowerCheckResult
    {
        public bool PowerOn { get; set; }
        public double Voltage { get; set; }
        public double MinVoltage { get; set; }
        public bool HasAnomaly { get; set; }
        public string AnomalyType { get; set; }
        public string AnomalyMessage { get; set; }
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

        // 目前步驟（供設備異常時回報是哪一步出錯）
        private int _currentStepNumber;
        private string _currentStepName = "";

        public event EventHandler<StepStartedEventArgs> StepStarted;
        public event EventHandler<string> RelayChanged;
        public event EventHandler<MeasurementEventArgs> Measured;
        public event EventHandler<TestStepResult> StepCompleted;
        public event EventHandler<string> StatusChanged;

        /// <summary>除錯日誌（可為 null）；用於記錄各 Step 的等待時間。</summary>
        public DebugLog Log { get; set; }

        public DX01TestFlow(IRelayController relay, IGdm8261AController meter)
        {
            _relay = relay;
            _meter = meter;
        }

        private static AppSettings Cfg { get { return AppSettings.Current; } }

        private void LogInfo(string msg)
        {
            if (Log != null) Log.Write(LogKind.Info, msg);
        }

        /// <summary>記錄並等待某 Step 的 StepNWaitMs（0 = 記錄 skip 不等待）。</summary>
        private async Task WaitStep(int step, CancellationToken token)
        {
            int ms = Cfg.WaitMs(step);
            LogInfo(ms > 0 ? "Step" + step + " WaitMs = " + ms : "Step" + step + " WaitMs = 0, skip delay");
            await Delay(ms, token);
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
            ResetPause();   // 確保不殘留上一輪的暫停狀態

            try
            {
                // Step 1 初始化電表：Relay=00、電阻模式、Auto 檔位（Relay 由下方狀態欄顯示，標題不重複）
                RaiseStep(1, "初始化電表", RangeText(MeasurementMode.Resistance));
                _meter.Reset();
                SwitchRelay("00");
                _meter.SetMode(MeasurementMode.Resistance);
                _meter.SetRangeAuto();
                await WaitStep(1, token);
                AddInfoStep(result, 1, "初始化電表", "00", "電阻", RangeText(MeasurementMode.Resistance), "OK");

                // Step 2 掃描 Label，記錄序號（序號已由 UI 傳入；標題不附序號避免過長 / 多餘標點）
                RaiseStep(2, "掃描 Label / 序號", "-");
                await WaitStep(2, token);
                AddInfoStep(result, 2, "掃描 Label / 記錄序號", "-", "-", "-", "OK");

                // V2.1：Step3~Step10 任一 NG 不再中止流程，記錄 NG 後繼續執行後續 Step，
                // 跑完 Step10 才由 Step11 最終判定（任一非資訊步驟 NG → Final Result = NG）。
                // 僅「設備 / 通訊異常」（例外）才會在下方 catch 立即停止。

                // Step 3 外殼對機殼導通：Relay=00，R < 10Ω
                await MeasureStep(result, 3, "外殼對機殼導通", "00",
                        MeasurementMode.Resistance, "Ω", null, Cfg.Step3CaseToChassisMax, token);

                // Step 4 P+ 對外殼絕緣：Relay=01，R > 1MΩ
                await MeasureStep(result, 4, "P+ 對外殼絕緣", "01",
                        MeasurementMode.Resistance, "Ω", Cfg.Step4PPlusInsulationMin, null, token);

                // Step 5 P- 對外殼絕緣：Relay=10，R > 1MΩ
                await MeasureStep(result, 5, "P- 對外殼絕緣", "10",
                        MeasurementMode.Resistance, "Ω", Cfg.Step5PMinusInsulationMin, null, token);

                // Step 6 切換電壓量測：DC Voltage、Range 由 Config（無判定值，仍列入結果顯示）
                string dcRangeLabel = Cfg.DcVoltageRange <= 0
                    ? "Auto"
                    : Cfg.DcVoltageRange.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture) + "V";
                RaiseStep(6, "切換電壓量測 (" + dcRangeLabel + " Range)", RangeText(MeasurementMode.DcVoltage));
                _meter.SetDcVoltageModeWithRange(Cfg.DcVoltageRange);
                await WaitStep(6, token);
                AddInfoStep(result, 6, "切換電壓量測", "-", "DC電壓", RangeText(MeasurementMode.DcVoltage), "OK");

                // Step 7 電壓總值：Relay=11，V > 45V（Step7WaitMs 於 READ 前等待，待電壓穩定）。
                await MeasureStep(result, 7, "電壓總值", "11",
                        MeasurementMode.DcVoltage, "V", Cfg.Step7TotalVoltageMin, null, token);

                // Step 8 P+ / P- 電壓：Relay=11，48V ~ 51V
                await MeasureStep(result, 8, "P+ / P- 電壓", "11",
                        MeasurementMode.DcVoltage, "V", Cfg.Step8PPlusMinusMin, Cfg.Step8PPlusMinusMax, token);

                // Step 9 P+ 對外殼電壓：Relay=01，V < 1V
                await MeasureStep(result, 9, "P+ 對外殼電壓", "01",
                        MeasurementMode.DcVoltage, "V", null, Cfg.Step9PPlusToCaseMax, token);

                // Step 10 P- 對外殼電壓：Relay=10，V < 1V
                await MeasureStep(result, 10, "P- 對外殼電壓", "10",
                        MeasurementMode.DcVoltage, "V", null, Cfg.Step10PMinusToCaseMax, token);

                result.Completed = true;
            }
            catch (OperationCanceledException)
            {
                result.Aborted = true;
            }
            catch (Exception ex)
            {
                // 設備 / 網路異常：立即停止流程，記錄為 FAIL 步驟並標記異常（由 UI 顯示 Popup）。
                string type = DeviceAnomaly.Classify(ex, AppSettings.Current.ConnectionMode == GdmConnectionMode.Lan);
                result.HasAnomaly = true;
                result.AnomalyType = type;
                result.AnomalyMessage = ex.Message;
                result.AnomalyStep = _currentStepNumber;
                result.AnomalyStepName = _currentStepName;
                AddErrorStep(result, _currentStepNumber, _currentStepName, type, ex.Message);
            }

            return Finish(result);
        }

        /// <summary>
        /// 正式測試前的產品 Power ON 檢查：DC 電壓模式（Cfg.DcVoltageRange）、Relay=11、READ?。
        /// 電壓 &gt;= minVoltage 視為已開機。檢查後一律將 Relay 復位 00。
        /// 不建立 TestResult；設備 / 通訊異常時回報 HasAnomaly，交由 UI 處理。
        /// </summary>
        public async Task<PowerCheckResult> CheckPowerOnAsync(double minVoltage, CancellationToken token)
        {
            var r = new PowerCheckResult { MinVoltage = minVoltage };
            try
            {
                RaiseStatus("Power ON 檢查…");
                RaiseStep(0, "Power ON 檢查", RangeText(MeasurementMode.DcVoltage));
                _meter.SetDcVoltageModeWithRange(Cfg.DcVoltageRange);
                SwitchRelay("11");
                await Delay(Cfg.RelaySwitchDelayMs, token);
                double v = _meter.Read();
                Measured?.Invoke(this, new MeasurementEventArgs(v, "V"));
                r.Voltage = v;
                r.PowerOn = v >= minVoltage;

                // 檢查後還原 Relay 至安全狀態（不論 ON / OFF）
                try { SwitchRelay("00"); } catch { }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                r.HasAnomaly = true;
                r.AnomalyType = DeviceAnomaly.Classify(ex, AppSettings.Current.ConnectionMode == GdmConnectionMode.Lan);
                r.AnomalyMessage = ex.Message;
            }
            return r;
        }

        /// <summary>記錄一個資訊步驟（無量測值），寫入結果並通知 UI。judgement 可覆寫結果欄（如 Step6 顯示 OK）。</summary>
        private void AddInfoStep(TestResult result, int step, string name, string relayCode, string mode, string range, string judgement = null)
        {
            var stepResult = new TestStepResult
            {
                StepNumber = step,
                StepName = name,
                RelayCode = relayCode,
                Mode = mode,
                Range = range,
                IsInfo = true,
                Pass = true,
                IgnoreInOverall = true, // 資訊步驟（Step1/2/6）不參與 PASS / NG 判定
                JudgementOverride = judgement,
                Time = DateTime.Now
            };
            result.Steps.Add(stepResult);
            StepCompleted?.Invoke(this, stepResult);
        }

        /// <summary>記錄一個設備異常步驟（NG，計入 FAIL 並寫入 CSV / 通知 UI）。</summary>
        private void AddErrorStep(TestResult result, int step, string name, string errorType, string errorMessage)
        {
            var stepResult = new TestStepResult
            {
                StepNumber = step,
                StepName = string.IsNullOrEmpty(name) ? "設備異常" : name,
                RelayCode = "-",
                Mode = "-",
                Pass = false,
                ErrorType = errorType,
                ErrorMessage = errorMessage,
                Time = DateTime.Now
            };
            result.Steps.Add(stepResult);
            StepCompleted?.Invoke(this, stepResult);
        }

        /// <summary>
        /// 執行一個量測步驟並判定，內含 NG 自動複測（最多 3 次）。
        /// 順序：① 切換 GDM 模式 ② 切換 Relay ③ 等待 RelaySwitchDelayMs ④ READ? ⑤ 判定。
        /// 主表格只回報「一列」（整併後的最終結果，NG/PASS 後附加 (Retry N)），
        /// 完整重試明細存於該列的 <see cref="TestStepResult.Attempts"/>，供 CSV / Debug Log / 內部紀錄。
        /// postPassWaitMs：PASS 後再等待的時間（Step7 用 Step7WaitMs）。
        /// </summary>
        private async Task<bool> MeasureStep(
            TestResult result, int step, string name, string relayCode,
            MeasurementMode mode, string unit, double? low, double? high,
            CancellationToken token)
        {
            const int maxAttempts = 3; // 第 1 次 + 最多 2 次複測（Retry 0 / 1 / 2）
            string modeText = mode == MeasurementMode.Resistance ? "電阻" : "DC電壓";
            string rangeText = RangeText(mode);

            var attempts = new List<TestStepResult>();
            bool finalPass = false;
            double finalValue = 0;
            int finalIndex = 0;

            for (int i = 0; i < maxAttempts; i++) // i = 0-based 嘗試索引（= Retry 編號）
            {
                // 暫停檢查點①：進入此步驟（含複測）前 → 暫停則停在目前 Step，不切換到下一步
                await WaitWhilePausedAsync(token);

                // 標題只顯示 動作名稱（+Retry）；Relay 由下方「Relay 狀態」欄顯示，不放進標題避免過長
                string desc = i == 0 ? name : name + " Retry " + i;
                RaiseStep(step, desc, RangeText(mode));

                // ① 切換 GDM 檔位 / 模式（DC 電壓固定檔位，避免被 auto 蓋掉）
                if (mode == MeasurementMode.DcVoltage)
                    _meter.SetDcVoltageModeWithRange(Cfg.DcVoltageRange);
                else
                    _meter.SetMode(mode);
                SwitchRelay(relayCode);                     // ② 切換 Relay
                LogInfo("RelaySwitchDelayMs = " + Cfg.RelaySwitchDelayMs);
                await Delay(Cfg.RelaySwitchDelayMs, token); // ③ 等待繼電器穩定
                await WaitStep(step, token);                // ④ 等待 StepNWaitMs

                // 暫停檢查點②：等待 StepWaitMs 完成後、READ? 之前 → 暫停在此生效（READ? 進行中則待其完成）
                await WaitWhilePausedAsync(token);

                double value = _meter.Read();               // ⑤ READ?
                Measured?.Invoke(this, new MeasurementEventArgs(value, unit));
                bool pass = Evaluate(value, low, high);      // ⑤ 判定

                attempts.Add(new TestStepResult
                {
                    StepNumber = step,
                    StepName = name,
                    RelayCode = relayCode,
                    Mode = modeText,
                    Range = rangeText,
                    Value = value,
                    Unit = unit,
                    LowLimit = low,
                    HighLimit = high,
                    Pass = pass,
                    Attempt = i,
                    Time = DateTime.Now
                });

                finalPass = pass;
                finalValue = value;
                finalIndex = i;

                if (pass)
                    break;
                // NG → 自動複測（迴圈），連續 maxAttempts 次才判 NG
            }

            // 整併為單一列：主表格只顯示一列；Attempts 保留完整重試明細。
            var display = new TestStepResult
            {
                StepNumber = step,
                StepName = name,
                RelayCode = relayCode,
                Mode = modeText,
                Range = rangeText,
                Value = finalValue,
                Unit = unit,
                LowLimit = low,
                HighLimit = high,
                Pass = finalPass,
                RetryCount = finalIndex, // 0=首次即定案；>0 顯示 (Retry N)
                Attempts = attempts,
                Time = DateTime.Now
            };
            result.Steps.Add(display);
            StepCompleted?.Invoke(this, display);

            return finalPass;
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
            // 確認 Relay 切換成功：控制器於成功寫入後才更新 CurrentCode；不符即視為 Relay 異常。
            if (_relay.CurrentCode != code)
                throw new InvalidOperationException(
                    "Relay 切換確認失敗（期望 " + code + "，實際 " + _relay.CurrentCode + "）。");
            RelayChanged?.Invoke(this, code);
        }

        private void RaiseStep(int step, string description, string range = "")
        {
            _currentStepNumber = step;
            _currentStepName = description;
            StepStarted?.Invoke(this, new StepStartedEventArgs(step, description, range));
        }

        /// <summary>Range 顯示字串：電阻=Auto；DC 電壓=DcVoltageRange（0=Auto，否則 "NV"）。</summary>
        private string RangeText(MeasurementMode mode)
        {
            if (mode == MeasurementMode.Resistance)
                return "Auto";
            return Cfg.DcVoltageRange <= 0
                ? "Auto"
                : Cfg.DcVoltageRange.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture) + "V";
        }

        private void RaiseStatus(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        // ===== 暫停 / 繼續 =====
        private readonly object _pauseLock = new object();
        private TaskCompletionSource<bool> _resumeTcs;

        /// <summary>目前是否暫停中。</summary>
        public bool IsPaused { get; private set; }

        /// <summary>暫停流程：在下一個暫停檢查點停住，不執行後續步驟。</summary>
        public void Pause()
        {
            lock (_pauseLock)
            {
                if (IsPaused) return;
                IsPaused = true;
                _resumeTcs = new TaskCompletionSource<bool>();
            }
            RaiseStatus("暫停中…");
        }

        /// <summary>從暫停處繼續（不重新開始、不清空已完成結果）。</summary>
        public void Resume()
        {
            TaskCompletionSource<bool> tcs;
            lock (_pauseLock)
            {
                if (!IsPaused) return;
                IsPaused = false;
                tcs = _resumeTcs;
                _resumeTcs = null;
            }
            RaiseStatus("測試中…");
            tcs?.TrySetResult(true);
        }

        /// <summary>每次流程開始時重置暫停狀態，確保不殘留上一輪的暫停 / 取消。</summary>
        private void ResetPause()
        {
            lock (_pauseLock)
            {
                IsPaused = false;
                _resumeTcs = null;
            }
        }

        /// <summary>暫停檢查點：暫停則等待至 Resume；期間若取消（停止）則丟出 OperationCanceledException。</summary>
        private async Task WaitWhilePausedAsync(CancellationToken token)
        {
            Task wait;
            lock (_pauseLock)
            {
                if (!IsPaused || _resumeTcs == null)
                    return;
                wait = _resumeTcs.Task;
            }

            using (token.Register(() => { lock (_pauseLock) { _resumeTcs?.TrySetCanceled(); } }))
            {
                await wait; // Resume → 正常返回；Stop(cancel) → OperationCanceledException
            }
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
            // 測試結束（Step11/12）復位 Relay = 00，回到安全狀態；UI 透過 RelayChanged 同步顯示 00
            try
            {
                if (_relay.IsConnected && _relay.CurrentCode != "00")
                    SwitchRelay("00");
            }
            catch { /* 設備已斷線等情況忽略 */ }

            result.EndTime = DateTime.Now;
            RaiseStatus(result.Aborted ? "已中止" : (result.IsPass ? "測試完成：OK" : "測試完成：NG"));
            return result;
        }
    }
}
