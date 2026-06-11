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
        public double Value { get; set; }
        public string Unit { get; set; }
        public double? LowLimit { get; set; }
        public double? HighLimit { get; set; }
        public bool Pass { get; set; }
        public DateTime Time { get; set; }

        /// <summary>資訊步驟（如 Step1 初始化、Step2 掃描），無量測值 / 無判定條件。</summary>
        public bool IsInfo { get; set; }

        /// <summary>量測值的易讀字串（自動換算 kΩ / MΩ）。</summary>
        public string ValueText
        {
            get { return IsInfo ? "—" : FormatValue(Value, Unit); }
        }

        /// <summary>判定條件文字，例如 "&lt; 10 Ω"、"48 ~ 51 V"。</summary>
        public string LimitText
        {
            get
            {
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
            get { return IsInfo ? "—" : (Pass ? "PASS" : "NG"); }
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
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        /// <summary>是否跑完全部步驟（未被中止）。</summary>
        public bool Completed { get; set; }

        /// <summary>是否被使用者中止。</summary>
        public bool Aborted { get; set; }

        public List<TestStepResult> Steps { get; } = new List<TestStepResult>();

        /// <summary>全部步驟通過且完整跑完才算 PASS。</summary>
        public bool IsPass
        {
            get { return Completed && !Aborted && Steps.Count > 0 && Steps.All(s => s.Pass); }
        }

        /// <summary>整體判定字串：OK / NG / 中止。</summary>
        public string Judgement
        {
            get
            {
                if (Aborted)
                    return "中止";
                return IsPass ? "OK" : "NG";
            }
        }

        /// <summary>第一個失敗的步驟（沒有則為 null）。</summary>
        public TestStepResult FirstFailedStep
        {
            get { return Steps.FirstOrDefault(s => !s.Pass); }
        }
    }
}
