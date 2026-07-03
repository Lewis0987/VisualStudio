using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace DX01_ShortCircuitTester.Services
{
    /// <summary>前站 Airflow PASS 檢查結果（供 Debug Log 與判定使用）。</summary>
    public sealed class AirflowCheckResult
    {
        public bool Pass;
        public string Url;
        public int HttpStatus;         // 0 = 無回應
        public string RawResponse;     // API 原始回傳內容
        public string ParsedValue;     // 解析後的 count / status 值（或 "[]"）
        public string FailReason;      // FAIL 原因（API timeout / HTTP error / response = [] / count = 0 / JSON parse failed ...）
    }

    /// <summary>
    /// 前站 Airflow PASS 檢查：GET {ApiUrl}?sn[]={barcode}。
    /// 只有「HTTP 成功 + 非空陣列 + 解析出的 count/status 值 != 0」才判定 PASS；其餘一律 FAIL。
    /// 使用 HttpWebRequest（免額外組件參考）；呼叫端請於背景執行緒呼叫以免卡 UI。
    /// </summary>
    public static class AirflowCheck
    {
        public static AirflowCheckResult Check(string baseUrl, string barcode, int timeoutMs)
        {
            var r = new AirflowCheckResult();
            if (string.IsNullOrEmpty(baseUrl) || baseUrl.Trim().Length == 0)
            {
                r.FailReason = "API URL not configured";
                return r;
            }
            if (timeoutMs <= 0) timeoutMs = 5000;

            string sep = baseUrl.IndexOf('?') >= 0 ? "&" : "?";
            r.Url = baseUrl.Trim() + sep + "sn[]=" + Uri.EscapeDataString(barcode ?? "");

            try
            {
                var req = (HttpWebRequest)WebRequest.Create(r.Url);
                req.Method = "GET";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.AllowAutoRedirect = true;

                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    r.HttpStatus = (int)resp.StatusCode;
                    r.RawResponse = ReadBody(resp);
                    if (r.HttpStatus < 200 || r.HttpStatus >= 300)
                    {
                        r.FailReason = "HTTP error " + r.HttpStatus;
                        return r;
                    }
                    Evaluate(r);
                    return r;
                }
            }
            catch (WebException wex)
            {
                var hr = wex.Response as HttpWebResponse;
                if (hr != null)
                {
                    r.HttpStatus = (int)hr.StatusCode;
                    try { r.RawResponse = ReadBody(hr); } catch { }
                    r.FailReason = "HTTP error " + r.HttpStatus;
                }
                else
                {
                    r.FailReason = (wex.Status == WebExceptionStatus.Timeout)
                        ? "API timeout" : ("API error: " + wex.Message);
                }
                return r;
            }
            catch (Exception ex)
            {
                r.FailReason = "API error: " + ex.Message;
                return r;
            }
        }

        private static string ReadBody(HttpWebResponse resp)
        {
            using (var s = resp.GetResponseStream())
            {
                if (s == null) return "";
                using (var sr = new StreamReader(s))
                    return sr.ReadToEnd();
            }
        }

        /// <summary>判定 PASS：非空陣列、且能解析出 count/status 值且 != 0。</summary>
        private static void Evaluate(AirflowCheckResult r)
        {
            string body = (r.RawResponse ?? "").Trim();
            if (body.Length == 0)
            {
                r.FailReason = "empty response";
                return;
            }

            // 空陣列 / 空物件 → FAIL（查無前站 PASS 紀錄）
            string compact = Regex.Replace(body, "\\s", "");
            if (compact == "[]" || compact == "{}")
            {
                r.ParsedValue = "[]";
                r.FailReason = "response = []";
                return;
            }

            long value;
            if (!TryExtractCount(body, out value))
            {
                r.FailReason = "JSON parse failed";
                return;
            }

            r.ParsedValue = value.ToString();
            if (value == 0)
            {
                r.FailReason = "count = 0";
                return;
            }

            r.Pass = true;   // 非空 + count/status != 0 → 前站 Airflow 已 PASS
        }

        /// <summary>從回傳字串抽取 count / status 數值（支援物件內鍵值、或純數字 / [數字]）。</summary>
        private static bool TryExtractCount(string body, out long value)
        {
            value = 0;

            // 1) "count"/"status"/"total"/"cnt"/"qty"/"pass" : <number>（數字可含引號）
            Match m = Regex.Match(body,
                "\"(?:count|status|total|cnt|qty|pass)\"\\s*:\\s*\"?(-?\\d+)\"?",
                RegexOptions.IgnoreCase);
            if (m.Success) return long.TryParse(m.Groups[1].Value, out value);

            // 2) 純數字或 [數字]（例："5"、"[5]"、"[ 5 ]"）
            m = Regex.Match(body, "^\\s*\\[?\\s*(-?\\d+)\\s*\\]?\\s*$");
            if (m.Success) return long.TryParse(m.Groups[1].Value, out value);

            return false;
        }
    }
}
