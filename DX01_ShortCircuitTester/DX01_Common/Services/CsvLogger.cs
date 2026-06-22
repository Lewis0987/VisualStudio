using System;
using System.IO;
using System.Text;
using DX01_ShortCircuitTester.Models;

namespace DX01_ShortCircuitTester.Services
{
    /// <summary>
    /// 測試結果 CSV 紀錄器（V2.3 寬表格式）。
    /// 每天一個檔案（Logs\DX01_yyyyMMdd.csv），每次測試完成（PASS / FAIL 皆）新增「一列」。
    /// 欄位固定為：TIME,Label,G1G2R,G1P+R,G1P-R,P+P-V,G1P+V,G2P-V。
    /// 量測值採目前程式顯示格式（含單位 / OL）；該次未量測之欄位留空（不填 0，避免誤判）。
    /// 完整重試明細仍記於 Debug Log（不受此格式影響）。
    /// </summary>
    public static class CsvLogger
    {
        private static readonly object _lock = new object();

        /// <summary>紀錄資料夾，預設為執行檔旁的 Logs。</summary>
        public static string LogDirectory { get; set; } =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        /// <summary>固定 CSV 表頭（欄位順序不可變更）。</summary>
        private const string Header = "TIME,Label,G1G2R,G1P+R,G1P-R,P+P-V,G1P+V,G2P-V";

        // 各 CSV 量測欄位對應的流程步驟編號：
        //   G1G2R = Step3 外殼對外殼導通 (Ω)
        //   G1P+R = Step4 P+ 對外殼絕緣 (Ω/MΩ)
        //   G1P-R = Step5 P- 對外殼絕緣 (Ω/MΩ)
        //   P+P-V = Step8 P+ / P- 電壓 (V)
        //   G1P+V = Step9 P+ 對外殼電壓 (V)
        //   G2P-V = Step10 P- 對外殼電壓 (V)
        // 註：Step7 總電壓僅作流程判斷，不獨立成欄。
        private const int StepG1G2R = 3;
        private const int StepG1PPlusR = 4;
        private const int StepG1PMinusR = 5;
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
            sb.Append(Csv(MeasureCell(result, StepG1PPlusR))).Append(',');
            sb.Append(Csv(MeasureCell(result, StepG1PMinusR))).Append(',');
            sb.Append(Csv(MeasureCell(result, StepPPlusMinusV))).Append(',');
            sb.Append(Csv(MeasureCell(result, StepG1PPlusV))).Append(',');
            sb.Append(Csv(MeasureCell(result, StepG2PMinusV)));
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
        /// 取得指定步驟的最終量測值（目前程式顯示格式，含單位 / OL）。
        /// 找不到、資訊步驟、或該步驟為設備異常（無量測值）→ 回傳空字串（留空，不填 0）。
        /// </summary>
        private static string MeasureCell(TestResult result, int stepNumber)
        {
            foreach (var s in result.Steps)
            {
                if (s.StepNumber != stepNumber) continue;
                if (s.IsInfo) continue;                                  // 資訊步驟無量測值
                if (s.ErrorType != null || string.IsNullOrEmpty(s.Unit)) continue; // 設備異常 / 未量測
                return TestStepResult.FormatMeasureValue(s.Value, s.Unit);
            }
            return "";
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
