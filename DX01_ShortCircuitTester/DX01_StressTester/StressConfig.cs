using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace DX01_StressTester
{
    /// <summary>
    /// 壓力測試單一 Step 設定（對應 UI 13 欄位）。
    /// 此設定僅供 DX01_StressTester 使用，與 OP 正式測試程式完全獨立。
    /// </summary>
    public sealed class StressStep
    {
        public int StepNo { get; set; }
        public string StepName { get; set; }
        public bool Enable { get; set; }

        /// <summary>Relay 通道：None / 1 / 2 / Both。</summary>
        public string RelayChannel { get; set; }
        /// <summary>Relay 狀態（true=ON, false=OFF）。</summary>
        public bool RelayOn { get; set; }

        /// <summary>量測模式：Voltage / Current / Resistance。</summary>
        public string MeasureMode { get; set; }

        /// <summary>判定下限（null=不檢查）。</summary>
        public double? MinValue { get; set; }
        /// <summary>判定上限（null=不檢查）。</summary>
        public double? MaxValue { get; set; }

        /// <summary>單位：V / mA / A / Ω（顯示用）。</summary>
        public string Unit { get; set; }

        public int WaitMs { get; set; }
        public int RetryCount { get; set; }
        public bool StopOnFail { get; set; }

        public string Remark { get; set; }

        /// <summary>是否為量測判定步驟（有設定上限或下限）。</summary>
        public bool IsMeasured
        {
            get { return MinValue.HasValue || MaxValue.HasValue; }
        }
    }

    /// <summary>壓力測試 Step 設定集合，序列化至 Config\StressTestConfig.json。</summary>
    public sealed class StressConfig
    {
        public List<StressStep> Steps { get; set; }

        public StressConfig()
        {
            Steps = new List<StressStep>();
        }

        /// <summary>Config 檔路徑：與 EXE 同層的 Config\StressTestConfig.json。</summary>
        public static string ConfigPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "StressTestConfig.json"); }
        }

        /// <summary>讀取設定；檔案不存在或解析失敗則回傳預設值。</summary>
        public static StressConfig Load()
        {
            try
            {
                string path = ConfigPath;
                if (!File.Exists(path))
                    return Default();

                using (FileStream fs = File.OpenRead(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(StressConfig));
                    var cfg = ser.ReadObject(fs) as StressConfig;
                    if (cfg == null || cfg.Steps == null || cfg.Steps.Count == 0)
                        return Default();
                    return cfg;
                }
            }
            catch
            {
                return Default();
            }
        }

        /// <summary>儲存設定到 Config\StressTestConfig.json（縮排格式）。</summary>
        public void Save()
        {
            string path = ConfigPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using (var ms = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, true, true, "  "))
                {
                    var ser = new DataContractJsonSerializer(typeof(StressConfig));
                    ser.WriteObject(writer, this);
                    writer.Flush();
                }
                File.WriteAllBytes(path, ms.ToArray());
            }
        }

        /// <summary>預設 Step1~Step12（對應 OP 流程語意，使用者可於 UI 調整）。</summary>
        public static StressConfig Default()
        {
            var c = new StressConfig();
            c.Steps.Add(Step(1, "初始化電表", true, "None", false, "Resistance", null, null, "Ω", 0, 0, true, "Relay=00 / 電阻 / Auto"));
            c.Steps.Add(Step(2, "掃描 Label / 序號", true, "None", false, "Resistance", null, null, "Ω", 0, 0, true, "資訊步驟"));
            c.Steps.Add(Step(3, "外殼對機殼導通", true, "None", false, "Resistance", null, 10, "Ω", 0, 2, true, "R < 10Ω"));
            c.Steps.Add(Step(4, "P+ 對外殼絕緣", true, "2", true, "Resistance", 1000000, null, "Ω", 0, 2, true, "R > 1MΩ"));
            c.Steps.Add(Step(5, "P- 對外殼絕緣", true, "1", true, "Resistance", 1000000, null, "Ω", 0, 2, true, "R > 1MΩ"));
            c.Steps.Add(Step(6, "切換電壓量測", true, "None", false, "Voltage", null, null, "V", 0, 0, true, "資訊步驟"));
            c.Steps.Add(Step(7, "電壓總值", true, "Both", true, "Voltage", 45, null, "V", 10000, 2, true, "V > 45V"));
            c.Steps.Add(Step(8, "P+ / P- 電壓", true, "Both", true, "Voltage", 48, 51, "V", 0, 2, true, "48 ~ 51V"));
            c.Steps.Add(Step(9, "P+ 對外殼電壓", true, "2", true, "Voltage", null, 1, "V", 0, 2, true, "V < 1V"));
            c.Steps.Add(Step(10, "P- 對外殼電壓", true, "1", true, "Voltage", null, 1, "V", 0, 2, true, "V < 1V"));
            c.Steps.Add(Step(11, "Final Result", true, "None", false, "Resistance", null, null, "Ω", 0, 0, true, "資訊步驟"));
            c.Steps.Add(Step(12, "Return Step1", true, "None", false, "Resistance", null, null, "Ω", 0, 0, true, "資訊步驟"));
            return c;
        }

        private static StressStep Step(int no, string name, bool enable, string ch, bool on,
            string mode, double? min, double? max, string unit, int waitMs, int retry, bool stopOnFail, string remark)
        {
            return new StressStep
            {
                StepNo = no,
                StepName = name,
                Enable = enable,
                RelayChannel = ch,
                RelayOn = on,
                MeasureMode = mode,
                MinValue = min,
                MaxValue = max,
                Unit = unit,
                WaitMs = waitMs,
                RetryCount = retry,
                StopOnFail = stopOnFail,
                Remark = remark
            };
        }
    }
}
