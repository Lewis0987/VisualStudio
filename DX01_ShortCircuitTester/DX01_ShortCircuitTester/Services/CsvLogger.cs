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
            "時間,序號,整體判定,步驟,步驟名稱,Relay,模式,量測值,單位,下限,上限,步驟判定";

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
                sb.Append(Csv(step.Time.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',');
                sb.Append(Csv(result.SerialNumber)).Append(',');
                sb.Append(Csv(result.Judgement)).Append(',');
                sb.Append(step.StepNumber).Append(',');
                sb.Append(Csv(step.StepName)).Append(',');
                sb.Append(Csv(step.RelayCode)).Append(',');
                sb.Append(Csv(step.Mode)).Append(',');
                sb.Append(Csv(FormatNumber(step.Value))).Append(',');
                sb.Append(Csv(step.Unit)).Append(',');
                sb.Append(Csv(step.LowLimit.HasValue ? FormatNumber(step.LowLimit.Value) : "")).Append(',');
                sb.Append(Csv(step.HighLimit.HasValue ? FormatNumber(step.HighLimit.Value) : "")).Append(',');
                sb.Append(Csv(step.Judgement));
                sb.AppendLine();
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
