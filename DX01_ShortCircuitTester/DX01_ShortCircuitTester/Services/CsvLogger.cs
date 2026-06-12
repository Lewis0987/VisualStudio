using System;
using System.IO;
using System.Text;
using DX01_ShortCircuitTester.Models;

namespace DX01_ShortCircuitTester.Services
{
    /// <summary>
    /// 測試結果 CSV 紀錄器。
    /// 每天一個檔案（Logs\DX01_yyyyMMdd.csv），每個步驟一列，
    /// 記錄 SN、時間、步驟、量測值、判定結果（含整體判定）。
    /// </summary>
    public static class CsvLogger
    {
        private static readonly object _lock = new object();

        /// <summary>紀錄資料夾，預設為執行檔旁的 Logs。</summary>
        public static string LogDirectory { get; set; } =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        private const string Header =
            "時間,序號,整體判定,步驟,步驟名稱,嘗試,Relay,模式,量測值,單位,下限,上限,步驟判定,RetryCount,ErrorType,ErrorMessage";

        /// <summary>將一次測試結果（所有步驟）附加寫入當日 CSV。回傳寫入的檔案路徑。</summary>
        public static string Append(TestResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            Directory.CreateDirectory(LogDirectory);

            string file = Path.Combine(
                LogDirectory,
                "DX01_" + result.StartTime.ToString("yyyyMMdd") + ".csv");

            var sb = new StringBuilder();
            foreach (var step in result.Steps)
            {
                // 量測步驟：展開每次嘗試（Retry 0/1/2）為獨立列，保留完整重試紀錄。
                if (step.Attempts != null && step.Attempts.Count > 0)
                {
                    foreach (var attempt in step.Attempts)
                        WriteRow(sb, result, attempt, "Retry " + attempt.Attempt);
                }
                else
                {
                    // 資訊步驟（Step1/2/6）：單一列。
                    WriteRow(sb, result, step, "");
                }
            }

            lock (_lock)
            {
                bool isNew = !File.Exists(file);
                if (isNew)
                {
                    // 加 UTF-8 BOM，讓 Excel 正確顯示中文
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

        /// <summary>輸出單一 CSV 列；attemptLabel 為重試標記（如 "Retry 0"），資訊步驟為空字串。</summary>
        private static void WriteRow(StringBuilder sb, TestResult result, TestStepResult row, string attemptLabel)
        {
            sb.Append(Csv(row.Time.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',');
            sb.Append(Csv(result.SerialNumber)).Append(',');
            sb.Append(Csv(result.Judgement)).Append(',');
            sb.Append(row.StepNumber).Append(',');
            sb.Append(Csv(row.StepName)).Append(',');
            sb.Append(Csv(attemptLabel)).Append(',');
            sb.Append(Csv(row.RelayCode)).Append(',');
            sb.Append(Csv(row.Mode)).Append(',');
            sb.Append(Csv(row.IsInfo ? "" : FormatNumber(row.Value))).Append(',');
            sb.Append(Csv(row.IsInfo ? "" : row.Unit)).Append(',');
            sb.Append(Csv(row.LowLimit.HasValue ? FormatNumber(row.LowLimit.Value) : "")).Append(',');
            sb.Append(Csv(row.HighLimit.HasValue ? FormatNumber(row.HighLimit.Value) : "")).Append(',');
            sb.Append(Csv(row.Judgement)).Append(',');
            // RetryCount：嘗試列用該次的 Retry 編號；其餘列用步驟的重試次數
            sb.Append(attemptLabel.Length > 0 ? row.Attempt : row.RetryCount).Append(',');
            sb.Append(Csv(row.ErrorType ?? "")).Append(',');
            sb.Append(Csv(row.ErrorMessage ?? ""));
            sb.AppendLine();
        }

        private static string FormatNumber(double v)
        {
            return v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
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
