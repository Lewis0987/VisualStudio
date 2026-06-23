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

        /// <summary>V2.3：需作業員動作的指示訊息（請將電池 Power 開機 / 關機），顯示於「目前步驟」大字。</summary>
        public event EventHandler<string> InstructionChanged;

        /// <summary>除錯日誌（可為 null）；用於記錄各 Step 的等待時間。</summary>
        public DebugLog Log { get; set; }

        public DX01TestFlow(IRelayController relay, IGdm8261AController meter)
        {
            _relay = relay;
            _meter = meter;
        }

        // 測試開始時對設定做快照：測試中即使修改 / 儲存 Settings，也只影響下一次測試。
        private AppSettings _cfgSnapshot;
        private AppSettings Cfg { get { return _cfgSnapshot ?? AppSettings.Current; } }

        private void LogInfo(string msg)
        {
            if (Log != null) Log.Write(LogKind.Info, msg);
        }

        /// <summary>等待 Power ON/OFF 的單筆狀態行（畫面原地更新、不寫檔）。</summary>
        private void LogStatus(string msg)
        {
            if (Log != null) Log.Write(LogKind.Status, msg);
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
            _cfgSnapshot = AppSettings.Current.Clone();   // 快照設定：測試中改參數只影響下一次測試

            try
            {
                // V2.3 測試前：等待 Power OFF（Turn off the battery，V <= PowerOffThreshold）才開始；逾時 → NG 停止
                if (!await WaitForPowerAsync(false, 0, result, true, token))
                    return Finish(result);

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

                // V2.3：任一量測 Step 判定 NG → 立即停止流程，不執行後續 Step（最終結果 FAIL）。
                // 設備 / 通訊異常（例外）亦於下方 catch 立即停止並標記異常（FAIL）。

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

                // V2.3 Step 6 先等待 Power ON（Turn on the battery，V >= PowerOnThreshold）才繼續；逾時 → NG 停止
                if (!await WaitForPowerAsync(true, 6, result, true, token))
                    return Finish(result);

                // Step 6 切換電壓量測：DC Voltage、Range 由 Config（無判定值，仍列入結果顯示）
                string dcRangeLabel = Cfg.DcVoltageRange <= 0
                    ? "Auto"
                    : Cfg.DcVoltageRange.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture) + "V";
                RaiseStep(6, "切換電壓量測 (" + dcRangeLabel + " Range)", RangeText(MeasurementMode.DcVoltage));
                _meter.SetDcVoltageModeWithRange(Cfg.DcVoltageRange);
                await WaitStep(6, token);
                AddInfoStep(result, 6, "切換電壓量測", "-", "DC電壓", RangeText(MeasurementMode.DcVoltage), "OK");

                // Step 7 電壓總值：Relay=11，V > 45V（Step7WaitMs 於 READ 前等待，待電壓穩定）。
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

            // V2.3 PASS 後：等待 Power OFF（請將電池 Power 關機）再返回待機。
            // 在判定鎖定後執行：此處按「停止」或設備斷線僅返回待機，不影響已判定的 PASS 結果。
            // FAIL / NG / 中止 / 設備異常 皆不進入（IsPass 為 false）→ 維持目前停止流程，等待人工處理。
            if (result.IsPass)
            {
                try
                {
                    // PASS 後等待 Power OFF；failOnTimeout=false → 逾時僅返回待機，不影響已判定的 PASS。
                    await WaitForPowerAsync(false, 12, result, false, token);
                }
                catch (OperationCanceledException) { /* 操作員停止：保留 PASS，僅返回待機 */ }
                catch { /* 等待期間設備異常：忽略，不影響已判定的 PASS */ }
            }

            return Finish(result);
        }

        /// <summary>
        /// V2.3：等待作業員開 / 關電池 Power。以 DC 電壓模式（Cfg.DcVoltageRange）、Relay=11 持續量測，
        /// 直到達到門檻（回傳 true）或逾時 PowerWaitTimeoutSec（回傳 false）。
        /// waitForOn=true：等待 V &gt;= PowerOnThreshold（請開機）；false：等待 V &lt;= PowerOffThreshold（請關機）。
        /// failOnTimeout=true（前置 / Step6 等待）：逾時時新增一筆 NG 結果列（判定條件 Power ON/OFF Timeout）並回傳 false，
        /// 由呼叫端立即停止流程（NG）。failOnTimeout=false（PASS 後等待）：逾時僅返回待機，不影響已判定的 PASS。
        /// PowerWaitTimeoutSec &lt;= 0 視為無限等待（只能由停止中斷）。暫停期間逾時計時暫停。
        /// 設備 / 通訊異常（READ 失敗）以例外往上拋，由 RunAsync 統一處理。
        /// </summary>
        private async Task<bool> WaitForPowerAsync(bool waitForOn, int stepNumber,
            TestResult result, bool failOnTimeout, CancellationToken token)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            double threshold = waitForOn ? Cfg.PowerOnThreshold : Cfg.PowerOffThreshold;
            string waitLabel = waitForOn ? "Waiting for Power ON" : "Waiting for Power OFF";

            // 供設備異常回報用（不經 RaiseStep，避免在主表格新增列 / 顯示 "Step N —" 標題）
            _currentStepNumber = stepNumber;
            _currentStepName = waitLabel;

            // V2.3：「目前步驟」紅字直接顯示等待狀態 + Timeout 倒數（不再顯示 "Turn on/off the battery." 以免重複提示）；
            // 底部狀態列只保留一般狀態（測試中）。
            RaiseInstruction(waitLabel + "...");
            RaiseStatus("測試中…");
            LogInfo(waitLabel + " (threshold " + (waitForOn ? ">= " : "<= ") +
                    threshold.ToString("0.###", ci) + "V)");

            _meter.SetDcVoltageModeWithRange(Cfg.DcVoltageRange);
            SwitchRelay("11");
            await Delay(Cfg.RelaySwitchDelayMs, token);

            int interval = Cfg.PowerPollIntervalMs > 0 ? Cfg.PowerPollIntervalMs : 500;
            long logIntervalMs = (long)(Cfg.PowerWaitLogIntervalSec > 0 ? Cfg.PowerWaitLogIntervalSec : 30) * 1000;
            long timeoutMs = (long)(Cfg.PowerWaitTimeoutSec > 0 ? Cfg.PowerWaitTimeoutSec : 0) * 1000;   // 0 = 無限
            var sw = System.Diagnostics.Stopwatch.StartNew();
            double nextLogMs = 0;
            int lastShownSec = -1;

            while (true)
            {
                token.ThrowIfCancellationRequested();

                // 暫停中：停止偵測與倒數，逾時計時亦暫停；恢復後重新計時並立即重畫。
                if (IsPaused)
                {
                    sw.Stop();
                    LogInfo("Test paused. Power detection paused.");
                    await WaitWhilePausedAsync(token);
                    RaiseInstruction(waitLabel + "...");   // 恢復後重新顯示於「目前步驟」（下一輪補上 Timeout 秒數）
                    sw.Start();
                    nextLogMs = sw.Elapsed.TotalMilliseconds;
                    lastShownSec = -1;
                }

                double v = _meter.ReadQuiet();
                Measured?.Invoke(this, new MeasurementEventArgs(v, "V"));

                if (waitForOn ? (v >= threshold) : (v <= threshold))
                    return await PowerDetectedAsync(waitForOn, v, token);

                // 逾時 → 套用既有 NG 流程（畫面直接顯示 NG，不顯示 "timeout" 字樣；Debug Log 記 "... timeout → NG"）。
                if (timeoutMs > 0 && sw.Elapsed.TotalMilliseconds >= timeoutMs)
                {
                    LogInfo(waitForOn ? "Power ON timeout → NG" : "Power OFF timeout → NG");
                    if (failOnTimeout && result != null)
                    {
                        var row = new TestStepResult
                        {
                            StepNumber = stepNumber,
                            StepName = waitForOn ? "Power ON" : "Power OFF",
                            RelayCode = "11",
                            Mode = "DC電壓",
                            Range = RangeText(MeasurementMode.DcVoltage),
                            Value = v,
                            Unit = "V",
                            LowLimit = waitForOn ? (double?)threshold : null,
                            HighLimit = waitForOn ? null : (double?)threshold,
                            Pass = false,                                       // 結果欄 → NG
                            Time = DateTime.Now
                        };
                        result.Steps.Add(row);
                        StepCompleted?.Invoke(this, row);
                    }
                    return false;
                }

                // 倒數顯示：每秒更新「Timeout: Ns」（不影響 PollIntervalMs 偵測 / PowerWaitLogIntervalSec Log）。
                if (timeoutMs > 0)
                {
                    int remainSec = (int)Math.Ceiling((timeoutMs - sw.Elapsed.TotalMilliseconds) / 1000.0);
                    if (remainSec < 0) remainSec = 0;
                    if (remainSec != lastShownSec)
                    {
                        lastShownSec = remainSec;
                        // 「目前步驟」紅字兩行：Waiting for Power ON... / Timeout: Ns
                        RaiseInstruction(waitLabel + "...\nTimeout: " + remainSec + "s");
                    }
                }

                // 等待狀態 Log：每 PowerWaitLogIntervalSec 才輸出一次（避免洗版）。
                if (sw.Elapsed.TotalMilliseconds >= nextLogMs)
                {
                    LogStatus(waitLabel + "... Voltage=" + v.ToString("0.0000", ci) + "V");
                    nextLogMs = sw.Elapsed.TotalMilliseconds + logIntervalMs;
                }

                await Delay(interval, token);
            }
        }

        /// <summary>偵測成功：顯示「Power ON/OFF detected.」約 1.5 秒後再進入下一步。</summary>
        private async Task<bool> PowerDetectedAsync(bool waitForOn, double v, CancellationToken token)
        {
            string detected = waitForOn ? "Power ON detected." : "Power OFF detected.";
            LogInfo(detected + " (" + v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "V)");
            RaiseInstruction(detected);   // 顯示於「目前步驟」（紅字）
            await Delay(1500, token);     // 顯示 1~2 秒後進入下一步
            return true;
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
        /// 執行一個量測步驟並判定（V2.3：NG 改用「時間窗」重試，取代固定 3 次）。
        /// 順序：① 切換 GDM 模式 ② 切換 Relay ③ 等待 RelaySwitchDelayMs ④ StepNWaitMs ⑤ READ?（loud）⑥ 判定。
        /// 若 NG：於 Cfg.NgRetryTimeoutMs 內每 Cfg.NgRetryIntervalMs 以「靜默讀取」重新量測，
        ///   期間恢復正常即判 PASS；逾時仍不符才判 NG。重試期間以單行狀態（原地更新）顯示，避免 Debug Log 洗版。
        /// 主表格只回報「一列」（最終結果，重試後附加 (Retry N)）；完整明細存於 <see cref="TestStepResult.Attempts"/>。
        /// 注意：此重試僅適用一般量測 Step，不適用 Power ON/OFF 等待（後者為無限等待）。
        /// </summary>
        private async Task<bool> MeasureStep(
            TestResult result, int step, string name, string relayCode,
            MeasurementMode mode, string unit, double? low, double? high,
            CancellationToken token)
        {
            string modeText = mode == MeasurementMode.Resistance ? "電阻" : "DC電壓";
            string rangeText = RangeText(mode);
            var attempts = new List<TestStepResult>();

            // 暫停檢查點①：進入此步驟前
            await WaitWhilePausedAsync(token);
            RaiseStep(step, name, rangeText);

            // ① 切換 GDM 檔位 / 模式（DC 電壓固定檔位，避免被 auto 蓋掉）② 切換 Relay ③ 等待繼電器穩定 ④ StepNWaitMs
            if (mode == MeasurementMode.DcVoltage)
                _meter.SetDcVoltageModeWithRange(Cfg.DcVoltageRange);
            else
                _meter.SetMode(mode);
            SwitchRelay(relayCode);
            LogInfo("RelaySwitchDelayMs = " + Cfg.RelaySwitchDelayMs);
            await Delay(Cfg.RelaySwitchDelayMs, token);
            await WaitStep(step, token);

            // 暫停檢查點②：READ? 之前
            await WaitWhilePausedAsync(token);

            // ⑤ 第一次量測（loud，保留 TX/RX）⑥ 判定
            double value = _meter.Read();
            Measured?.Invoke(this, new MeasurementEventArgs(value, unit));
            bool pass = Evaluate(value, low, high);
            attempts.Add(MakeAttempt(step, name, relayCode, modeText, rangeText, value, unit, low, high, pass, 0));

            int retryCount = 0;
            int timeoutMs = Cfg.NgRetryTimeoutMs < 0 ? 0 : Cfg.NgRetryTimeoutMs;
            int intervalMs = Cfg.NgRetryIntervalMs > 0 ? Cfg.NgRetryIntervalMs : 300;

            if (!pass && timeoutMs > 0)
            {
                // NG → 時間窗重試：靜默讀取（不寫 TX/RX）＋單行狀態（原地更新），避免洗版。
                LogInfo("Step" + step + " 量測 NG（" + TestStepResult.FormatValue(value, unit) +
                        "），進入重試時間窗 " + timeoutMs + "ms（每 " + intervalMs + "ms 重測）");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                while (sw.Elapsed.TotalMilliseconds < timeoutMs)
                {
                    await WaitWhilePausedAsync(token);   // 暫停則停在此（時間窗於暫停期間不前進視為 best-effort）
                    await Delay(intervalMs, token);

                    value = _meter.ReadQuiet();          // 重試靜默讀取（無 TX/RX）
                    Measured?.Invoke(this, new MeasurementEventArgs(value, unit));
                    pass = Evaluate(value, low, high);
                    retryCount++;
                    attempts.Add(MakeAttempt(step, name, relayCode, modeText, rangeText, value, unit, low, high, pass, retryCount));

                    LogStatus("Step" + step + " 重試中... " + TestStepResult.FormatValue(value, unit) +
                              " (" + (int)sw.Elapsed.TotalMilliseconds + "/" + timeoutMs + "ms)");

                    if (pass)
                        break;
                }

                if (pass)
                    LogInfo("Step" + step + " 重試 " + retryCount + " 次後 PASS（" +
                            TestStepResult.FormatValue(value, unit) + "，" + (int)sw.Elapsed.TotalMilliseconds + "ms）");
                else
                    LogInfo("Step" + step + " 重試逾時 NG（" + TestStepResult.FormatValue(value, unit) +
                            "，" + timeoutMs + "ms）");
            }

            // 整併為單一列：主表格只顯示一列；Attempts 保留完整重試明細。
            var display = new TestStepResult
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
                RetryCount = retryCount, // 0=首次即定案；>0 顯示 (Retry N)
                Attempts = attempts,
                Time = DateTime.Now
            };
            result.Steps.Add(display);
            StepCompleted?.Invoke(this, display);

            return pass;
        }

        /// <summary>建立一筆重試明細（供 Attempts 紀錄）。</summary>
        private static TestStepResult MakeAttempt(
            int step, string name, string relayCode, string modeText, string rangeText,
            double value, string unit, double? low, double? high, bool pass, int attempt)
        {
            return new TestStepResult
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
                Attempt = attempt,
                Time = DateTime.Now
            };
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

        private void RaiseInstruction(string message)
        {
            InstructionChanged?.Invoke(this, message);
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
            _cfgSnapshot = null;   // 釋放本次快照（下一次測試開始時重新擷取）
            RaiseStatus(result.Aborted ? "已中止" : (result.IsPass ? "測試完成：OK" : "測試完成：NG"));
            return result;
        }
    }
}
