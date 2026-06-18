using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DX01_ShortCircuitTester.Device;

namespace DX01_ShortCircuitTester.Services
{
    /// <summary>
    /// 全專案單一設定來源（記憶體 + Config\DX01Config.json）。
    /// SettingForm 編輯 / 儲存；MainForm、Real 設備、DX01TestFlow 皆讀取 <see cref="Current"/>。
    /// net48 無內建 JSON 函式庫且不變更 csproj，故以極簡解析讀取、手動序列化寫入。
    /// </summary>
    public sealed class AppSettings
    {
        // 1. 設備連線
        public GdmConnectionMode ConnectionMode = GdmConnectionMode.Lan;
        public string Ip = "192.168.100.100";
        public int TcpPort = 23;
        public int ComBaud = 115200;
        public int VendorId = 0x16C0;
        public int ProductId = 0x05DF;
        public string DebugLevel = "debug"; // error / info / debug

        // 2. 條碼 / 序號規則
        public string BarcodeRegex = "^DX01[0-9]{8}$";

        // 3. 電阻條件
        public double Step3CaseToChassisMax = 10;          // IRUpper
        public double Step4PPlusInsulationMin = 1000000;   // OLValue
        public double Step5PMinusInsulationMin = 1000000;  // OLValue

        // 4. 電壓條件
        public double Step7TotalVoltageMin = 45;   // VoltOn
        public double Step8PPlusMinusMin = 48;      // VoltLower
        public double Step8PPlusMinusMax = 51;      // VoltUpper
        public double Step9PPlusToCaseMax = 1;      // VoltIsoUpper
        public double Step10PMinusToCaseMax = 1;    // VoltIsoUpper
        public double DcVoltageRange = 100;         // GDM DC 電壓檔位 (CONF:VOLT:DC <range>)

        // 5. 電流條件（流程未使用，保留）
        public double CurrentMin = 0;
        public double CurrentMax = 0;

        // 6. Step 等待時間（index 1..10）；找不到=0
        public int[] StepWaitMs = { 0, 0, 0, 0, 0, 0, 0, 10000, 0, 0, 0 };

        // 7. UI / 執行參數
        public int PopupSeconds = 3;
        public int StepFontSize = 20;
        public int PollIntervalMs = 100;
        public int ReadTimeoutMs = 5000;
        public int RelaySwitchDelayMs = 300;

        // 8. LAN 重新連線參數
        public int ConnectTimeoutMs = 3000;   // TCP 連線逾時
        public int ReconnectDelayMs = 500;     // 每次重試前的等待
        public int ReconnectRetryCount = 3;    // 重試次數

        // 9. Debug Log 顯示上限（行數），超過移除最舊
        public int MaxDebugLogLines = 2000;

        // 10. GDM LAN 背景斷線偵測間隔（毫秒）
        public int GdmMonitorIntervalMs = 1000;

        /// <summary>全域目前設定。</summary>
        public static AppSettings Current = new AppSettings();

        public string VendorIdHex { get { return "0x" + VendorId.ToString("X4"); } }
        public string ProductIdHex { get { return "0x" + ProductId.ToString("X4"); } }

        public int WaitMs(int step)
        {
            return (step >= 1 && step <= 10) ? StepWaitMs[step] : 0;
        }

        /// <summary>複製目前設定（供測試開始時快照，使測試中修改參數不影響當前流程）。</summary>
        public AppSettings Clone()
        {
            var c = (AppSettings)MemberwiseClone();
            if (StepWaitMs != null)
                c.StepWaitMs = (int[])StepWaitMs.Clone();
            return c;
        }

        // ===================== 載入 =====================

        public static void Load()
        {
            var s = new AppSettings();
            string path = FindConfigFile();
            if (path != null)
            {
                try
                {
                    string json = File.ReadAllText(path);

                    string mode = Str(json, "gdm_connectionMode", "Lan");
                    s.ConnectionMode = mode.Equals("Serial", StringComparison.OrdinalIgnoreCase)
                        ? GdmConnectionMode.Serial : GdmConnectionMode.Lan;
                    s.Ip = Str(json, "gdm_ip", s.Ip);
                    s.TcpPort = (int)Num(json, "gdm_tcpPort", s.TcpPort);
                    s.ComBaud = (int)Num(json, "gdm_comBaud", s.ComBaud);
                    s.VendorId = Hex(json, "vendorIdHex", s.VendorId);
                    s.ProductId = Hex(json, "productIdHex", s.ProductId);
                    s.DebugLevel = Str(json, "debugLevel", s.DebugLevel);

                    s.BarcodeRegex = Str(json, "barcodeRegex", s.BarcodeRegex);

                    s.Step3CaseToChassisMax = Num(json, "step3_caseToChassis_max", s.Step3CaseToChassisMax);
                    s.Step4PPlusInsulationMin = Num(json, "step4_pPlusInsulation_min", s.Step4PPlusInsulationMin);
                    s.Step5PMinusInsulationMin = Num(json, "step5_pMinusInsulation_min", s.Step5PMinusInsulationMin);
                    s.Step7TotalVoltageMin = Num(json, "step7_totalVoltage_min", s.Step7TotalVoltageMin);
                    s.Step8PPlusMinusMin = Num(json, "step8_pPlusMinusVoltage_min", s.Step8PPlusMinusMin);
                    s.Step8PPlusMinusMax = Num(json, "step8_pPlusMinusVoltage_max", s.Step8PPlusMinusMax);
                    s.Step9PPlusToCaseMax = Num(json, "step9_pPlusToCaseVoltage_max", s.Step9PPlusToCaseMax);
                    s.Step10PMinusToCaseMax = Num(json, "step10_pMinusToCaseVoltage_max", s.Step10PMinusToCaseMax);
                    s.DcVoltageRange = Num(json, "DcVoltageRange", s.DcVoltageRange); // 遺失=預設 100

                    s.CurrentMin = Num(json, "current_min", s.CurrentMin);
                    s.CurrentMax = Num(json, "current_max", s.CurrentMax);

                    // 找不到欄位 = 0
                    for (int i = 1; i <= 10; i++)
                        s.StepWaitMs[i] = (int)Num(json, "Step" + i + "WaitMs", 0);

                    s.PopupSeconds = (int)Num(json, "PopupSeconds", s.PopupSeconds);
                    s.StepFontSize = (int)Num(json, "StepFontSize", s.StepFontSize);
                    s.PollIntervalMs = (int)Num(json, "PollIntervalMs", s.PollIntervalMs);
                    s.ReadTimeoutMs = (int)Num(json, "ReadTimeoutMs", s.ReadTimeoutMs);
                    s.RelaySwitchDelayMs = (int)Num(json, "RelaySwitchDelayMs", s.RelaySwitchDelayMs);
                    s.ConnectTimeoutMs = (int)Num(json, "ConnectTimeoutMs", s.ConnectTimeoutMs);
                    s.ReconnectDelayMs = (int)Num(json, "ReconnectDelayMs", s.ReconnectDelayMs);
                    s.ReconnectRetryCount = (int)Num(json, "ReconnectRetryCount", s.ReconnectRetryCount);
                    s.MaxDebugLogLines = (int)Num(json, "MaxDebugLogLines", s.MaxDebugLogLines);
                    s.GdmMonitorIntervalMs = (int)Num(json, "GdmMonitorIntervalMs", s.GdmMonitorIntervalMs);
                }
                catch
                {
                    s = new AppSettings();
                }
            }
            Current = s;
        }

        // ===================== 儲存 =====================

        public bool Save(out string error)
        {
            error = null;
            try
            {
                string path = FindConfigFile();
                if (path == null)
                {
                    path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "DX01Config.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                }
                File.WriteAllText(path, ToJson(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public string ToJson()
        {
            var p = new List<string>();
            p.Add(Line("description", JStr("DX01 外殼短路流程設定 (V1.2)。由參數設定視窗或手動編輯；找不到欄位時 StepWaitMs=0、其餘用內建預設。")));
            p.Add(Line("gdm_connectionMode", JStr(ConnectionMode == GdmConnectionMode.Lan ? "Lan" : "Serial")));
            p.Add(Line("gdm_ip", JStr(Ip)));
            p.Add(Line("gdm_tcpPort", TcpPort.ToString(CultureInfo.InvariantCulture)));
            p.Add(Line("gdm_comBaud", ComBaud.ToString(CultureInfo.InvariantCulture)));
            p.Add(Line("vendorIdHex", JStr(VendorIdHex)));
            p.Add(Line("productIdHex", JStr(ProductIdHex)));
            p.Add(Line("debugLevel", JStr(DebugLevel)));
            p.Add(Line("barcodeRegex", JStr(BarcodeRegex)));
            p.Add(Line("step3_caseToChassis_max", Dbl(Step3CaseToChassisMax)));
            p.Add(Line("step4_pPlusInsulation_min", Dbl(Step4PPlusInsulationMin)));
            p.Add(Line("step5_pMinusInsulation_min", Dbl(Step5PMinusInsulationMin)));
            p.Add(Line("step7_totalVoltage_min", Dbl(Step7TotalVoltageMin)));
            p.Add(Line("step8_pPlusMinusVoltage_min", Dbl(Step8PPlusMinusMin)));
            p.Add(Line("step8_pPlusMinusVoltage_max", Dbl(Step8PPlusMinusMax)));
            p.Add(Line("step9_pPlusToCaseVoltage_max", Dbl(Step9PPlusToCaseMax)));
            p.Add(Line("step10_pMinusToCaseVoltage_max", Dbl(Step10PMinusToCaseMax)));
            p.Add(Line("DcVoltageRange", Dbl(DcVoltageRange)));
            p.Add(Line("current_min", Dbl(CurrentMin)));
            p.Add(Line("current_max", Dbl(CurrentMax)));
            for (int i = 1; i <= 10; i++)
                p.Add(Line("Step" + i + "WaitMs", StepWaitMs[i].ToString(CultureInfo.InvariantCulture)));
            p.Add(Line("PopupSeconds", PopupSeconds.ToString(CultureInfo.InvariantCulture)));
            p.Add(Line("StepFontSize", StepFontSize.ToString(CultureInfo.InvariantCulture)));
            p.Add(Line("PollIntervalMs", PollIntervalMs.ToString(CultureInfo.InvariantCulture)));
            p.Add(Line("ReadTimeoutMs", ReadTimeoutMs.ToString(CultureInfo.InvariantCulture)));
            p.Add(Line("RelaySwitchDelayMs", RelaySwitchDelayMs.ToString(CultureInfo.InvariantCulture)));
            p.Add(Line("ConnectTimeoutMs", ConnectTimeoutMs.ToString(CultureInfo.InvariantCulture)));
            p.Add(Line("ReconnectDelayMs", ReconnectDelayMs.ToString(CultureInfo.InvariantCulture)));
            p.Add(Line("ReconnectRetryCount", ReconnectRetryCount.ToString(CultureInfo.InvariantCulture)));
            p.Add(Line("MaxDebugLogLines", MaxDebugLogLines.ToString(CultureInfo.InvariantCulture)));
            p.Add(Line("GdmMonitorIntervalMs", GdmMonitorIntervalMs.ToString(CultureInfo.InvariantCulture)));

            return "{" + Environment.NewLine + string.Join("," + Environment.NewLine, p) + Environment.NewLine + "}" + Environment.NewLine;
        }

        private static string Line(string key, string valueJson)
        {
            return "  \"" + key + "\": " + valueJson;
        }

        private static string JStr(string s)
        {
            if (s == null) s = "";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string Dbl(double v)
        {
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        // ===================== 解析輔助 =====================

        private static string FindConfigFile()
        {
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                for (int i = 0; i < 7 && !string.IsNullOrEmpty(dir); i++)
                {
                    string c1 = Path.Combine(dir, "Config", "DX01Config.json");
                    if (File.Exists(c1)) return c1;

                    string c2 = Path.Combine(dir, "DX01_ShortCircuitTester", "Config", "DX01Config.json");
                    if (File.Exists(c2)) return c2;

                    dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
                }
            }
            catch { }
            return null;
        }

        private static double Num(string json, string key, double def)
        {
            Match m = Regex.Match(json,
                "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?(?:[eE][-+]?[0-9]+)?)");
            double v;
            if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;
            return def;
        }

        private static string Str(string json, string key, string def)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
            return m.Success ? m.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\") : def;
        }

        private static int Hex(string json, string key, int def)
        {
            string s = Str(json, key, null);
            if (string.IsNullOrEmpty(s)) return def;
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);
            int v;
            return int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v) ? v : def;
        }
    }
}
