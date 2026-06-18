using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace DX01_ShortCircuitTester.Services
{
    /// <summary>
    /// Debug Log 檔案寫入器（Logs\yyyyMMdd.log）。
    /// - 單檔超過 10MB 自動切檔（yyyyMMdd_1.log、yyyyMMdd_2.log…）。
    /// - 啟動時自動清除超過保留天數（預設 90 天）的舊檔。
    /// - 畫面清除不影響檔案內容。
    /// </summary>
    public sealed class LogFileWriter
    {
        private const long MaxBytes = 10L * 1024 * 1024; // 10MB
        private readonly object _lock = new object();

        public static string LogDirectory { get; set; } =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        /// <summary>附加一行（已含時間 / 類別 / 訊息）到當日 Log 檔（必要時自動切檔）。</summary>
        public void Append(string line)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(LogDirectory);
                    string path = CurrentFile();
                    File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch { /* 寫檔失敗不可中斷流程 */ }
        }

        /// <summary>取得當日要寫入的檔案；目前區段達 10MB 則往下一個 _N 區段。</summary>
        private static string CurrentFile()
        {
            string day = DateTime.Now.ToString("yyyyMMdd");
            string path = Path.Combine(LogDirectory, day + ".log");
            int seg = 0;
            while (File.Exists(path) && new FileInfo(path).Length >= MaxBytes)
            {
                seg++;
                path = Path.Combine(LogDirectory, day + "_" + seg + ".log");
            }
            return path;
        }

        /// <summary>清除超過保留天數的舊 Log（以檔名日期判斷，無法解析則用最後寫入時間）。</summary>
        public void CleanupOldLogs(int keepDays = 90)
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                    return;

                DateTime cutoff = DateTime.Now.Date.AddDays(-keepDays);
                foreach (string f in Directory.GetFiles(LogDirectory, "*.log"))
                {
                    try
                    {
                        string name = Path.GetFileNameWithoutExtension(f);
                        string datePart = name.Length >= 8 ? name.Substring(0, 8) : "";
                        DateTime fileDate;
                        if (!DateTime.TryParseExact(datePart, "yyyyMMdd",
                                CultureInfo.InvariantCulture, DateTimeStyles.None, out fileDate))
                            fileDate = File.GetLastWriteTime(f).Date;

                        if (fileDate < cutoff)
                            File.Delete(f);
                    }
                    catch { /* 個別檔案刪除失敗略過 */ }
                }
            }
            catch { }
        }
    }
}
