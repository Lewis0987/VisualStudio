using System;
using System.Threading;
using System.Threading.Tasks;
using DX01_ShortCircuitTester.Device;    // 共用設備（DX01_Common）
using DX01_ShortCircuitTester.Services;

namespace DX01_StressTester
{
    /// <summary>單一 Loop 的執行結果。</summary>
    internal enum LoopOutcome
    {
        Pass,         // 全部啟用步驟通過
        Fail,         // 有步驟 NG（已跑完 / 或 Continue Loop 提前結束本圈）
        Stopped,      // 要求停止整個壓力測試（StopOnFail 或 NG Action=Stop Test）
        DeviceError   // 設備 / 通訊異常 → 要求停止循環
    }

    /// <summary>全域 NG 行為（Step StopOnFail=false 時套用）。</summary>
    internal enum NgAction
    {
        StopTest,          // 任何 Step NG 立即停止整個壓力測試
        ContinueLoop,      // 記錄 FAIL 後跳過剩餘 Step，直接進入下一 Loop
        ContinueNextStep   // 記錄 FAIL 後繼續執行剩餘 Step
    }

    /// <summary>
    /// 依 <see cref="StressConfig"/> 逐步執行壓力測試。
    /// 通訊一律透過 DX01_Common 的 <see cref="IRelayController"/> / <see cref="IGdm8261AController"/>，
    /// 不複製通訊邏輯；序列 / 判定則由 Step 設定驅動（與 OP 的 DX01TestFlow 互不影響）。
    /// </summary>
    internal sealed class StressTestRunner
    {
        private readonly IRelayController _relay;
        private readonly IGdm8261AController _meter;

        /// <summary>Log 回呼 (tag, message)。</summary>
        public Action<string, string> Log;

        public StressTestRunner(IRelayController relay, IGdm8261AController meter)
        {
            _relay = relay;
            _meter = meter;
        }

        private void L(string tag, string msg)
        {
            var h = Log;
            if (h != null) h(tag, msg);
        }

        public async Task<LoopOutcome> RunLoopAsync(int loopIndex, StressConfig cfg, NgAction ngAction, CancellationToken token)
        {
            bool loopFailed = false;

            foreach (StressStep step in cfg.Steps)
            {
                token.ThrowIfCancellationRequested();

                if (!step.Enable)
                {
                    L("SKIP", "Step" + step.StepNo + " " + step.StepName + " (Disabled)");
                    continue;
                }

                try
                {
                    // 1) 切換 Relay（依 Channel + ON/OFF 組成代碼）
                    string code = RelayCode(step);
                    _relay.SetRelay(code);

                    // 2) 設定量測模式
                    bool current = Eq(step.MeasureMode, "Current");
                    bool resistance = Eq(step.MeasureMode, "Resistance");
                    if (!current)
                    {
                        if (resistance) _meter.SetMode(MeasurementMode.Resistance);
                        else _meter.SetDcVoltageModeWithRange(AppSettings.Current.DcVoltageRange);
                    }

                    // 3) 等待
                    if (step.WaitMs > 0)
                        await Task.Delay(step.WaitMs, token);

                    // 4) 非量測步驟（無上下限）→ 視為設定 / 資訊步驟，直接通過
                    if (!step.IsMeasured)
                    {
                        L("STEP", "Step" + step.StepNo + " " + step.StepName + " OK (Relay=" + code + ")");
                        continue;
                    }

                    // GDM 控制器目前未提供電流模式 → 記錄後略過判定（Phase 2 視需求擴充）
                    if (current)
                    {
                        L("WARN", "Step" + step.StepNo + " Current 模式尚未支援，略過判定");
                        continue;
                    }

                    // 5) 量測 + 判定（NG 自動複測 RetryCount 次）
                    int attempts = Math.Max(1, step.RetryCount + 1);
                    bool pass = false;
                    double val = 0;
                    for (int a = 0; a < attempts; a++)
                    {
                        token.ThrowIfCancellationRequested();
                        val = _meter.Read();
                        pass = Judge(val, step);
                        if (pass) break;
                        if (a < attempts - 1)
                            L("RETRY", "Step" + step.StepNo + " NG " + Fmt(val, step) +
                                " → retry " + (a + 1) + "/" + (attempts - 1));
                    }

                    if (pass)
                    {
                        L("PASS", "Step" + step.StepNo + " " + step.StepName + " = " + Fmt(val, step));
                    }
                    else
                    {
                        loopFailed = true;
                        L("FAIL", "Step" + step.StepNo + " " + step.StepName + " = " + Fmt(val, step) +
                            "  (limit " + LimitText(step) + ")");

                        // 優先順序：Step StopOnFail=true 優先停止；否則依全域 NG Action。
                        if (step.StopOnFail)
                        {
                            L("STOP", "Step" + step.StepNo + " StopOnFail=true → 停止整個壓力測試");
                            return LoopOutcome.Stopped;
                        }

                        if (ngAction == NgAction.StopTest)
                        {
                            L("STOP", "NG Action=Stop Test → 停止整個壓力測試");
                            return LoopOutcome.Stopped;
                        }
                        if (ngAction == NgAction.ContinueLoop)
                        {
                            L("NEXT", "NG Action=Continue Loop → 跳過剩餘 Step，進入下一 Loop");
                            return LoopOutcome.Fail;
                        }
                        // NgAction.ContinueNextStep → 記錄 FAIL 後繼續執行剩餘 Step
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // 交由上層處理「使用者停止」
                }
                catch (Exception ex)
                {
                    L("ERR", "Step" + step.StepNo + " 設備異常：" + ex.Message);
                    return LoopOutcome.DeviceError;
                }
            }

            return loopFailed ? LoopOutcome.Fail : LoopOutcome.Pass;
        }

        /// <summary>Channel(None/1/2/Both) + ON/OFF → Relay 兩位代碼（左=Relay1，右=Relay2）。</summary>
        private static string RelayCode(StressStep step)
        {
            char r1 = '0', r2 = '0';
            if (step.RelayOn)
            {
                string ch = step.RelayChannel ?? "None";
                if (ch == "1") r1 = '1';
                else if (ch == "2") r2 = '1';
                else if (Eq(ch, "Both")) { r1 = '1'; r2 = '1'; }
            }
            return new string(new[] { r1, r2 });
        }

        private static bool Judge(double v, StressStep step)
        {
            if (step.MinValue.HasValue && v < step.MinValue.Value) return false;
            if (step.MaxValue.HasValue && v > step.MaxValue.Value) return false;
            return true;
        }

        private static bool Eq(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static string Fmt(double v, StressStep step)
        {
            return v.ToString("0.######") + " " + (step.Unit ?? "");
        }

        private static string LimitText(StressStep step)
        {
            string u = step.Unit ?? "";
            if (step.MinValue.HasValue && step.MaxValue.HasValue)
                return step.MinValue.Value + " ~ " + step.MaxValue.Value + " " + u;
            if (step.MaxValue.HasValue) return "< " + step.MaxValue.Value + " " + u;
            if (step.MinValue.HasValue) return "> " + step.MinValue.Value + " " + u;
            return "-";
        }
    }
}
