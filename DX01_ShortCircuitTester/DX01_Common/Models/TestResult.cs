using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DX01_ShortCircuitTester.Models
{
    /// <summary>單一測試步驟的結果。</summary>
    public sealed class TestStepResult
    {
        public int StepNumber { get; set; }
        public string StepName { get; set; }
        public string RelayCode { get; set; }
        public string Mode { get; set; }
        public string Range { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public double? LowLimit { get; set; }
        public double? HighLimit { get; set; }
        public bool Pass { get; set; }
        public DateTime Time { get; set; }

        /// <summary>資訊步驟（如 Step1 初始化、Step2 掃描），無量測值 / 無判定條件。</summary>
        public bool IsInfo { get; set; }

        /// <summary>資訊步驟的結果文字覆寫（如 Step6 切換電壓量測顯示 OK）；null 時用預設邏輯。</summary>
        public string JudgementOverride { get; set; }

        /// <summary>NG 複測的非決定性嘗試（之後有 PASS 或非最終嘗試），不計入整體判定。</summary>
        public bool IgnoreInOverall { get; set; }

        /// <summary>重試次數（0 = 第一次即定案）；&gt;0 時結果欄附加 "(Retry N)"。</summary>
        public int RetryCount { get; set; }

        /// <summary>此筆為第幾次嘗試（0-based），供 CSV / Debug 的重試明細使用。</summary>
        public int Attempt { get; set; }

        /// <summary>量測步驟的每次嘗試明細（供 CSV / Debug Log / 內部紀錄）；資訊步驟為 null。</summary>
        public List<TestStepResult> Attempts { get; set; }

        /// <summary>設備異常類型（如 LAN Timeout / SetFeature Failed）；正常步驟為 null。</summary>
        public string ErrorType { get; set; }

        /// <summary>設備異常訊息；正常步驟為 null。</summary>
        public string ErrorMessage { get; set; }

        /// <summary>判定條件欄文字覆寫（如 Power 等待逾時顯示 "Power ON Timeout"）；null 時用上下限預設邏輯。</summary>
        public string LimitTextOverride { get; set; }

        /// <summary>量測值的 UI 顯示字串（精簡、≤約 10 碼；不影響判定與 CSV 原始值）。</summary>
        public string ValueText
        {
            get { return IsInfo ? "—" : FormatMeasureValue(Value, Unit); }
        }

        /// <summary>判定條件文字，例如 "&lt; 10 Ω"、"48 ~ 51 V"。</summary>
        public string LimitText
        {
            get
            {
                if (LimitTextOverride != null)
                    return LimitTextOverride;
                if (IsInfo)
                    return "—";
                if (LowLimit.HasValue && HighLimit.HasValue)
                    return FormatValue(LowLimit.Value, Unit) + " ~ " + FormatValue(HighLimit.Value, Unit);
                if (HighLimit.HasValue)
                    return "< " + FormatValue(HighLimit.Value, Unit);
                if (LowLimit.HasValue)
                    return "> " + FormatValue(LowLimit.Value, Unit);
                return "-";
            }
        }

        public string Judgement
        {
            get
            {
                if (JudgementOverride != null)
                    return JudgementOverride;
                if (IsInfo)
                    return "—";
                string r = Pass ? "PASS" : "NG";
                if (RetryCount > 0)
                    r += " (Retry " + RetryCount + ")";
                return r;
            }
        }

        /// <summary>
        /// UI 量測值精簡顯示（僅供畫面，不用於判定 / CSV 原始值）。
        /// 規則：溢位=OL；|值|&gt;=1e6 或 &lt;1e-3 用科學記號；其餘最多 4 位小數。字串長度盡量 ≤ 10 碼。
        /// </summary>
        public static string FormatMeasureValue(double value, string unit)
        {
            double abs = Math.Abs(value);

            // 溢位 / 無效 / 超大電阻 → OL
            if (double.IsNaN(value) || double.IsInfinity(value) || abs >= 9.9e37)
                return "OL " + unit;

            if (unit == "Ω")
            {
                // 電阻：工程單位顯示；>= 1TΩ 視為開路 (OL)
                if (abs >= 1e12) return "OL Ω";
                if (abs >= 1e6) return (value / 1e6).ToString("0.###", CultureInfo.InvariantCulture) + " MΩ";
                if (abs >= 1e3) return (value / 1e3).ToString("0.###", CultureInfo.InvariantCulture) + " kΩ";
                return value.ToString("0.###", CultureInfo.InvariantCulture) + " Ω";
            }

            // 電壓等：一般數值、固定 4 位小數，不使用科學記號（例 0.0001 / 0.0030 / 50.1235）
            return value.ToString("0.0000", CultureInfo.InvariantCulture) + " " + unit;
        }

        public static string FormatValue(double value, string unit)
        {
            if (unit == "Ω")
            {
                double abs = Math.Abs(value);
                if (abs >= 1e6)
                    return (value / 1e6).ToString("0.###", CultureInfo.InvariantCulture) + " MΩ";
                if (abs >= 1e3)
                    return (value / 1e3).ToString("0.###", CultureInfo.InvariantCulture) + " kΩ";
                return value.ToString("0.###", CultureInfo.InvariantCulture) + " Ω";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture) + " " + unit;
        }
    }

    /// <summary>一次完整測試（單一序號）的結果。</summary>
    public sealed class TestResult
    {
        public string SerialNumber { get; set; }

        /// <summary>執行此測試的操作者工號（人員追溯用；不含密碼）。</summary>
        public string OperatorId { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        /// <summary>是否跑完全部步驟（未被中止）。</summary>
        public bool Completed { get; set; }

        /// <summary>是否被使用者中止。</summary>
        public bool Aborted { get; set; }

        /// <summary>是否發生設備 / 網路異常（立即停止流程）。</summary>
        public bool HasAnomaly { get; set; }

        /// <summary>設備異常類型（分類字串，如 LAN Timeout）。</summary>
        public string AnomalyType { get; set; }

        /// <summary>設備異常原始訊息。</summary>
        public string AnomalyMessage { get; set; }

        /// <summary>發生異常時的步驟編號。</summary>
        public int AnomalyStep { get; set; }

        /// <summary>發生異常時的步驟名稱。</summary>
        public string AnomalyStepName { get; set; }

        public List<TestStepResult> Steps { get; } = new List<TestStepResult>();

        /// <summary>全部步驟通過且完整跑完才算 PASS。</summary>
        public bool IsPass
        {
            get { return Completed && !Aborted && Steps.Count > 0 && Steps.All(s => s.IgnoreInOverall || s.Pass); }
        }

        /// <summary>整體判定字串：PASS / NG / 中止。</summary>
        public string Judgement
        {
            get
            {
                if (Aborted)
                    return "中止";
                return IsPass ? "PASS" : "NG";
            }
        }

        /// <summary>第一個失敗的步驟（沒有則為 null）。</summary>
        public TestStepResult FirstFailedStep
        {
            get { return Steps.FirstOrDefault(s => !s.IgnoreInOverall && !s.Pass); }
        }
    }
}
