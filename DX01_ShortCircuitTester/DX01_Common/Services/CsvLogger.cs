using System;
using System.Globalization;
using System.IO;
using System.Text;
using DX01_ShortCircuitTester.Models;

namespace DX01_ShortCircuitTester.Services
{
    /// <summary>
    /// 測試結果 CSV 紀錄器（V2.5 寬表格式）。
    /// 每天一個檔案（Logs\DX01_yyyyMMdd.csv），每次測試完成（目前僅 PASS 會寫入）新增「一列」。
    /// 欄位固定為：
    ///   TIME,Label,G1G2R,G1P+R,G2P-R,P+P-V,G1P+V,G2P-V,G1P+R/G2P-RThreshold,Result
    /// 量測值一律「不含單位符號」（無 Ω / V / kΩ / MΩ）：
    ///   電阻 → 以 Ω 為基準的純數字（0.###，不做 k/M 換算）；電壓 → 0.0000。
    /// G1P+R / G2P-R 為絕緣測試，改以狀態文字輸出：超出量測範圍(OL) → "&gt;1000K"；通過 → "OK"。
    /// 該次未量測之欄位留空（不填 0，避免誤判）。完整重試明細仍記於 Debug Log（不受此格式影響）。
    /// 注意：本類別僅負責「CSV 顯示格式」，不參與量測、PASS / NG 判定或 Threshold 參數本身。
    /// </summary>
    public static class CsvLogger
    {
        private static readonly object _lock = new object();

        /// <summary>紀錄資料夾，預設為執行檔旁的 Logs。</summary>
        public static string LogDirectory { get; set; } =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        /// <summary>固定 CSV 表頭（欄位順序不可變更）。</summary>
        private const string Header =
            "TIME,Label,G1G2R,G1P+R,G2P-R,P+P-V,G1P+V,G2P-V,G1P+R/G2P-RThreshold,Result";

        /// <summary>絕緣量測超出範圍（原 OL）時的 CSV 顯示文字（不含單位）。</summary>
        private const string OverRangeText = ">1000K";

        // 各 CSV 量測欄位對應的流程步驟編號（測試流程與步驟編號皆未變更，僅 CSV 顯示名稱調整）：
        //   G1G2R = Step3 外殼對外殼導通（電阻）
        //   G1P+R = Step4 P+ 對外殼絕緣（電阻 → 狀態文字）
        //   G2P-R = Step5 P- 對外殼絕緣（電阻 → 狀態文字；舊 CSV 欄名為 G1P-R）
        //   P+P-V = Step8 P+ / P- 電壓
        //   G1P+V = Step9 P+ 對外殼電壓
        //   G2P-V = Step10 P- 對外殼電壓
        // 註：Step7 總電壓僅作流程判斷，不獨立成欄。
        private const int StepG1G2R = 3;
        private const int StepG1PPlusR = 4;
        private const int StepG2PMinusR = 5;
        private const int StepPPlusMinusV = 8;
        private const int StepG1PPlusV = 9;
        private const int StepG2PMinusV = 10;

        /// <summary>將一次測試結果以「一列」附加寫入當日 CSV。回傳寫入的檔案路徑。</summary>
        public static string Append(TestResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            Directory.CreateDirectory(LogDirectory);

            string file = Path.Combine(
                LogDirectory,
                "DX01_" + result.StartTime.ToString("yyyyMMdd") + ".csv");

            // TIME = 測試完成時間；Label = 條碼 / 序號；其餘為各量測欄位（未量測留空）。
            var sb = new StringBuilder();
            sb.Append(Csv(result.EndTime.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',');
            sb.Append(Csv(result.SerialNumber ?? "")).Append(',');
            sb.Append(Csv(MeasureCell(result, StepG1G2R))).Append(',');
            sb.Append(Csv(InsulationCell(result, StepG1PPlusR))).Append(',');
            sb.Append(Csv(InsulationCell(result, StepG2PMinusR))).Append(',');
            sb.Append(Csv(MeasureCell(result, StepPPlusMinusV))).Append(',');
            sb.Append(Csv(MeasureCell(result, StepG1PPlusV))).Append(',');
            sb.Append(Csv(MeasureCell(result, StepG2PMinusV))).Append(',');
            sb.Append(Csv(InsulationThresholdCell(result))).Append(',');
            sb.Append(Csv(result.Judgement ?? ""));
            sb.AppendLine();

            lock (_lock)
            {
                bool needHeader = true;
                if (File.Exists(file))
                {
                    if (ReadFirstLine(file) == Header)
                        needHeader = false;           // 同格式 → 直接附加新列
                    else
                        ArchiveLegacy(file);          // 舊格式 → 改名備份後以新格式重建（不刪除舊資料）
                }

                if (needHeader)
                {
                    // 新檔加 UTF-8 BOM，讓 Excel 正確顯示中文
                    using (var writer = new StreamWriter(file, false, new UTF8Encoding(true)))
                    {
                        writer.WriteLine(Header);
                        writer.Write(sb.ToString());
                    }
                }
                else
                {
                    File.AppendAllText(file, sb.ToString(), new UTF8Encoding(false));
                }
            }

            return file;
        }

        /// <summary>
        /// 取得指定步驟的最終量測步驟。
        /// 找不到、資訊步驟、或該步驟為設備異常（無量測值）→ 回傳 null。
        /// </summary>
        private static TestStepResult FindStep(TestResult result, int stepNumber)
        {
            foreach (var s in result.Steps)
            {
                if (s.StepNumber != stepNumber) continue;
                if (s.IsInfo) continue;                                  // 資訊步驟無量測值
                if (s.ErrorType != null || string.IsNullOrEmpty(s.Unit)) continue; // 設備異常 / 未量測
                return s;
            }
            return null;
        }

        /// <summary>
        /// 一般量測欄位（G1G2R / P+P-V / G1P+V / G2P-V）：輸出「不含單位」的原始量測值。
        /// 未量測 → 空字串（留空，不填 0）。
        /// </summary>
        private static string MeasureCell(TestResult result, int stepNumber)
        {
            var s = FindStep(result, stepNumber);
            return s == null ? "" : FormatNoUnit(s.Value, s.Unit);
        }

        /// <summary>
        /// 絕緣量測欄位（G1P+R / G2P-R）：不再輸出「OL Ω」，改為狀態文字。
        ///   超出量測範圍 (原 OL) → "&gt;1000K"
        ///   絕緣測試通過          → "OK"
        ///   其餘（NG，有實際數值）→ 不含單位的原始量測值（供追溯）
        /// 未量測 → 空字串。判定仍由測試流程決定，此處僅轉換顯示文字。
        /// </summary>
        private static string InsulationCell(TestResult result, int stepNumber)
        {
            var s = FindStep(result, stepNumber);
            if (s == null) return "";
            if (IsOverRange(s.Value, s.Unit)) return OverRangeText;
            if (s.Pass) return "OK";
            return FormatNoUnit(s.Value, s.Unit);
        }

        /// <summary>
        /// G1P+R / G2P-R 的判定門檻（沿用原參數設定值，不做任何換算 / 修改），不含單位。
        /// 兩步驟門檻相同 → 輸出單一數值；不同 → 以 "/" 併列（G1P+R/G2P-R 順序）。
        /// </summary>
        private static string InsulationThresholdCell(TestResult result)
        {
            string a = ThresholdOf(FindStep(result, StepG1PPlusR));
            string b = ThresholdOf(FindStep(result, StepG2PMinusR));

            if (a == b) return a;
            if (a.Length == 0) return b;
            if (b.Length == 0) return a;
            return a + "/" + b;
        }

        /// <summary>取得絕緣步驟的門檻設定值（LowLimit，即 "&gt; N" 條件），不含單位；無設定 → 空字串。</summary>
        private static string ThresholdOf(TestStepResult s)
        {
            if (s == null || !s.LowLimit.HasValue) return "";
            return s.LowLimit.Value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <summary>量測值是否為溢位 / 超出量測範圍（等同 UI 顯示的 OL；判定條件不受影響）。</summary>
        private static bool IsOverRange(double value, string unit)
        {
            double abs = Math.Abs(value);
            if (double.IsNaN(value) || double.IsInfinity(value) || abs >= 9.9e37)
                return true;
            return unit == "Ω" && abs >= 1e12;
        }

        /// <summary>
        /// CSV 專用數值格式（一律不含單位符號）。
        /// 電阻：以 Ω 為基準的純數字（最多 3 位小數，不做 kΩ / MΩ 換算，避免去單位後數值失真）。
        /// 電壓等：固定 4 位小數（與原顯示相同，僅去除單位）。
        /// 溢位 / 超出量測範圍 → "&gt;1000K"（避免輸出 9.9E+37 之類無意義數值）。
        /// </summary>
        private static string FormatNoUnit(double value, string unit)
        {
            if (IsOverRange(value, unit))
                return OverRangeText;

            if (unit == "Ω")
                return value.ToString("0.###", CultureInfo.InvariantCulture);

            return value.ToString("0.0000", CultureInfo.InvariantCulture);
        }

        /// <summary>讀取檔案第一行（自動處理 UTF-8 BOM）；讀取失敗回傳 null。</summary>
        private static string ReadFirstLine(string file)
        {
            try
            {
                using (var reader = new StreamReader(file, Encoding.UTF8, true))
                    return reader.ReadLine();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>舊格式檔案改名備份（非破壞性），讓當日檔名可重建為新格式。</summary>
        private static void ArchiveLegacy(string file)
        {
            string backup = file + ".legacy-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bak";
            File.Move(file, backup);
        }

        /// <summary>CSV 欄位跳脫：含逗號 / 引號 / 換行時用雙引號包起來。</summary>
        private static string Csv(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            bool mustQuote = field.IndexOf(',') >= 0 || field.IndexOf('"') >= 0 ||
                             field.IndexOf('\n') >= 0 || field.IndexOf('\r') >= 0;

            if (!mustQuote)
                return field;

            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
    }
}
