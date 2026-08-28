using System;
using System.IO;
using System.Text;
using DX01_ShortCircuitTester.Models;
using DX01_ShortCircuitTester.Services;

namespace DX01_CsvDemoGenerator
{
    /// <summary>
    /// CSV Demo 產生工具（開發輔助，非出貨程式）。
    ///
    /// 目的：不啟動 DX01_ShortCircuitTester.exe、不連 GDM / Relay、不執行測試流程，
    ///       即可產生一份「目前 CsvLogger 格式」的範例 CSV。
    ///
    /// 重點：本工具「不自行實作任何 CSV Header / 欄位格式」，
    ///       而是組出一筆假的 PASS <see cref="TestResult"/> 後直接呼叫正式的
    ///       <see cref="CsvLogger.Append(TestResult)"/>。
    ///       因此日後修改 DX01_Common\Services\CsvLogger.cs（Header 或輸出格式），
    ///       重新執行本工具即自動反映最新格式，無需同步維護第二套格式。
    ///
    /// 輸出：預設寫到「方案根目錄\Demo\Logs」，不會碰到正式的
    ///       DX01_ShortCircuitTester\bin\Debug\net48\Logs。
    ///       （CsvLogger.LogDirectory 本身就是公開可設定屬性，故不需修改 CsvLogger。）
    /// </summary>
    internal static class Program
    {
        /// <summary>GDM 對「超出量測範圍」的回傳值；正式流程即以此值判定 OL。</summary>
        private const double OverloadReading = 9.9e37;

        /// <summary>絕緣判定門檻：對應 Settings 的 OLValue → Step4 / Step5 的 LowLimit。</summary>
        private const double OlValue = 1000000;

        private static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;

                bool reset = false;
                string outDir = null;
                foreach (string a in args)
                {
                    if (string.Equals(a, "--reset", StringComparison.OrdinalIgnoreCase))
                        reset = true;
                    else if (!a.StartsWith("-"))
                        outDir = a;
                }

                if (string.IsNullOrEmpty(outDir))
                    outDir = Path.Combine(FindRepoRoot(), "Demo", "Logs");

                // 唯一需要的設定：把輸出導向 Demo\Logs（正式程式的預設行為完全不變）。
                CsvLogger.LogDirectory = outDir;

                TestResult demo = BuildDemoResult();

                string file = Path.Combine(outDir,
                    "DX01_" + demo.StartTime.ToString("yyyyMMdd") + ".csv");

                if (reset && File.Exists(file))
                {
                    File.Delete(file);
                    Console.WriteLine("--reset：已刪除既有 Demo 檔 " + Path.GetFileName(file));
                }

                // ★ 直接呼叫正式 CsvLogger，不自行組 CSV。
                string written = CsvLogger.Append(demo);

                Console.WriteLine("=== CSV Demo 產生完成 ===");
                Console.WriteLine(@"  來源格式  : DX01_Common\Services\CsvLogger.cs (CsvLogger.Append)");
                Console.WriteLine("  整體判定  : " + demo.Judgement + "  (IsPass=" + demo.IsPass + ")");
                Console.WriteLine("  輸出資料夾: " + outDir);
                Console.WriteLine("  Demo CSV  : " + written);
                Console.WriteLine();
                Console.WriteLine("=== Demo CSV 內容 ===");

                foreach (string line in File.ReadAllLines(written, Encoding.UTF8))
                    Console.WriteLine("  " + line);

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("CSV Demo 產生失敗: " + ex.Message);
                return 1;
            }
        }

        /// <summary>
        /// 組出一筆假的 PASS 測試結果。
        /// Step 編號與正式流程 <c>DX01TestFlow</c> 完全一致：
        ///   Step3 = G1G2R、Step4 = G1P+R、Step5 = G2P-R、
        ///   Step8 = P+P-V、Step9 = G1P+V、Step10 = G2P-V。
        /// </summary>
        private static TestResult BuildDemoResult()
        {
            DateTime now = DateTime.Now;

            var r = new TestResult
            {
                SerialNumber = "DX0100000000",
                OperatorId = "DEMO",
                StartTime = now,
                EndTime = now,
                Completed = true,
                Aborted = false
            };

            r.Steps.Add(Measure(3, "外殼對機殼導通", "00", "電阻", "Ω", 2.240, null, 10));
            // 絕緣量測回傳 OL（超出量測範圍）→ CsvLogger 轉為 ">1000K"
            r.Steps.Add(Measure(4, "P+ 對外殼絕緣", "01", "電阻", "Ω", OverloadReading, OlValue, null));
            r.Steps.Add(Measure(5, "P- 對外殼絕緣", "10", "電阻", "Ω", OverloadReading, OlValue, null));
            r.Steps.Add(Measure(8, "P+ / P- 電壓", "11", "DC電壓", "V", 49.8951, 48, 51));
            r.Steps.Add(Measure(9, "P+ 對外殼電壓", "01", "DC電壓", "V", 0.3268, null, 1));
            r.Steps.Add(Measure(10, "P- 對外殼電壓", "10", "DC電壓", "V", 0.3920, null, 1));

            return r;
        }

        /// <summary>建立一筆量測步驟；Pass 由 <see cref="Evaluate"/> 依上下限計算（與正式流程同邏輯）。</summary>
        private static TestStepResult Measure(int step, string name, string relay, string mode,
                                              string unit, double value, double? low, double? high)
        {
            return new TestStepResult
            {
                StepNumber = step,
                StepName = name,
                RelayCode = relay,
                Mode = mode,
                Range = "-",
                Value = value,
                Unit = unit,
                LowLimit = low,
                HighLimit = high,
                Pass = Evaluate(value, low, high),
                Time = DateTime.Now
            };
        }

        /// <summary>判定邏輯，與 DX01TestFlow.Evaluate 相同（僅供 Demo 資料自我一致，不影響正式判定）。</summary>
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

        /// <summary>由執行檔位置往上尋找含 DX01_ShortCircuitTester.sln 的方案根目錄。</summary>
        private static string FindRepoRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                if (File.Exists(Path.Combine(dir, "DX01_ShortCircuitTester.sln")))
                    return dir;
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            }
            throw new DirectoryNotFoundException(
                "找不到 DX01_ShortCircuitTester.sln，請以參數指定輸出資料夾。");
        }
    }
}
