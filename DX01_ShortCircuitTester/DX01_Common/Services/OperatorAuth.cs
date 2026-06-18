using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DX01_ShortCircuitTester.Services
{
    /// <summary>帳號角色。</summary>
    public enum OperatorRole
    {
        Operator,
        Admin
    }

    /// <summary>單一員工帳號（密碼僅存 SHA256 雜湊，不存明文）。</summary>
    [DataContract]
    public sealed class OperatorAccount
    {
        [DataMember(Name = "id", Order = 0)] public string Id { get; set; }
        [DataMember(Name = "role", Order = 1)] public string Role { get; set; }
        [DataMember(Name = "pwdSha256", Order = 2)] public string PwdSha256 { get; set; }
    }

    /// <summary>
    /// 員工登入與權限。工號固定 8 碼數字；密碼以 SHA256 雜湊比對（不保存 / 不記錄明文）。
    /// 帳號來源：Config\Operators.json（首次執行自動建立預設 Admin 00000000）。
    /// 此類僅處理驗證與狀態，UI 由 LoginForm / MainForm 負責。
    /// </summary>
    public sealed class OperatorAuth
    {
        private List<OperatorAccount> _accounts = new List<OperatorAccount>();

        public bool IsLoggedIn { get; private set; }
        public string OperatorId { get; private set; }
        public OperatorRole Role { get; private set; }
        public bool IsAdmin { get { return IsLoggedIn && Role == OperatorRole.Admin; } }

        /// <summary>帳號格式：固定 8 碼英文字母或數字（不含特殊符號 / 空白）。</summary>
        public static bool IsValidId(string id)
        {
            return Regex.IsMatch(id ?? "", "^[A-Za-z0-9]{8}$");
        }

        /// <summary>帳號格式錯誤訊息。</summary>
        public const string IdFormatError = "帳號格式錯誤，請輸入 8 碼英文字母或數字。";

        /// <summary>密碼格式：固定 8 碼英文字母或數字（不含特殊符號 / 空白）。</summary>
        public static bool IsValidPassword(string pwd)
        {
            return Regex.IsMatch(pwd ?? "", "^[A-Za-z0-9]{8}$");
        }

        public const string PwdFormatError = "密碼格式錯誤，請輸入 8 碼英文字母或數字。";

        /// <summary>目前登入者是否仍使用預設管理員密碼（供提示修改）。</summary>
        public bool UsingDefaultPassword { get; private set; }

        public static string ConfigPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Operators.json"); }
        }

        /// <summary>載入帳號檔；不存在或解析失敗則建立 / 使用預設 Admin。</summary>
        public void Load()
        {
            try
            {
                string path = ConfigPath;
                if (!File.Exists(path))
                {
                    _accounts = DefaultAccounts();
                    Save();
                    return;
                }

                using (FileStream fs = File.OpenRead(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(List<OperatorAccount>));
                    var list = ser.ReadObject(fs) as List<OperatorAccount>;
                    _accounts = (list != null && list.Count > 0) ? list : DefaultAccounts();
                }
            }
            catch
            {
                _accounts = DefaultAccounts();
            }
        }

        private void Save()
        {
            try
            {
                string path = ConfigPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var ms = new MemoryStream())
                {
                    using (var w = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, true, true, "  "))
                    {
                        var ser = new DataContractJsonSerializer(typeof(List<OperatorAccount>));
                        ser.WriteObject(w, _accounts);
                        w.Flush();
                    }
                    File.WriteAllBytes(path, ms.ToArray());
                }
            }
            catch { /* 寫檔失敗不影響登入流程 */ }
        }

        private static List<OperatorAccount> DefaultAccounts()
        {
            // 預設 Admin：帳號 admin000 / 密碼 admin000（建議首次使用後立即修改）
            return new List<OperatorAccount>
            {
                new OperatorAccount { Id = DefaultAdminId, Role = "Admin", PwdSha256 = Sha256Hex(DefaultAdminPassword) }
            };
        }

        /// <summary>嘗試登入；成功設定狀態並回 true，否則 error 回傳原因。</summary>
        public bool TryLogin(string id, string password, out string error)
        {
            error = "";
            id = (id ?? "").Trim();

            if (!IsValidId(id))
            {
                error = IdFormatError;
                return false;
            }

            OperatorAccount acc = _accounts.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));
            if (acc == null)
            {
                error = "查無此帳號。";
                return false;
            }

            if (!string.Equals(acc.PwdSha256 ?? "", Sha256Hex(password ?? ""), StringComparison.OrdinalIgnoreCase))
            {
                error = "密碼錯誤。";
                return false;
            }

            OperatorId = id;
            Role = string.Equals(acc.Role, "Admin", StringComparison.OrdinalIgnoreCase)
                ? OperatorRole.Admin : OperatorRole.Operator;
            IsLoggedIn = true;
            // 是否仍為預設管理員密碼（預設帳號 + 密碼雜湊等於預設）
            UsingDefaultPassword =
                string.Equals(id, DefaultAdminId, StringComparison.Ordinal) &&
                string.Equals(acc.PwdSha256 ?? "", Sha256Hex(DefaultAdminPassword), StringComparison.OrdinalIgnoreCase);
            return true;
        }

        public void Logout()
        {
            IsLoggedIn = false;
            OperatorId = null;
            Role = OperatorRole.Operator;
            UsingDefaultPassword = false;
        }

        // ===== 帳號管理（Admin 用）=====

        /// <summary>預設 Admin 帳號，受保護不可刪除。</summary>
        public const string DefaultAdminId = "admin000";
        /// <summary>預設 Admin 密碼（僅用於首次建檔與「是否仍為預設密碼」判斷）。</summary>
        public const string DefaultAdminPassword = "admin000";

        /// <summary>回傳帳號清單複本（工號 + 角色；不含密碼雜湊外洩風險，呼叫端僅讀 Id/Role）。</summary>
        public List<OperatorAccount> ListAccounts()
        {
            return _accounts
                .Select(a => new OperatorAccount { Id = a.Id, Role = a.Role, PwdSha256 = null })
                .ToList();
        }

        /// <summary>新增帳號（角色 Admin/Operator，密碼自動 SHA256）。</summary>
        public bool AddAccount(string id, string role, string password, out string error)
        {
            error = "";
            id = (id ?? "").Trim();
            if (!IsValidId(id)) { error = IdFormatError; return false; }
            if (!IsValidRole(role)) { error = "角色僅能為 Admin 或 Operator。"; return false; }
            if (!IsValidPassword(password)) { error = PwdFormatError; return false; }
            if (_accounts.Any(a => string.Equals(a.Id, id, StringComparison.Ordinal)))
            { error = "帳號已存在。"; return false; }

            _accounts.Add(new OperatorAccount
            {
                Id = id,
                Role = NormalizeRole(role),
                PwdSha256 = Sha256Hex(password)
            });
            Save();
            return true;
        }

        /// <summary>刪除帳號（預設 Admin 不可刪除）。</summary>
        public bool DeleteAccount(string id, out string error)
        {
            error = "";
            id = (id ?? "").Trim();
            if (string.Equals(id, DefaultAdminId, StringComparison.Ordinal))
            { error = "預設 Admin 帳號不可刪除。"; return false; }

            OperatorAccount acc = _accounts.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));
            if (acc == null) { error = "查無此帳號。"; return false; }

            _accounts.Remove(acc);
            Save();
            return true;
        }

        /// <summary>重設帳號密碼（自動 SHA256）。</summary>
        public bool ResetPassword(string id, string password, out string error)
        {
            error = "";
            id = (id ?? "").Trim();
            if (!IsValidPassword(password)) { error = PwdFormatError; return false; }

            OperatorAccount acc = _accounts.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));
            if (acc == null) { error = "查無此帳號。"; return false; }

            acc.PwdSha256 = Sha256Hex(password);
            Save();
            return true;
        }

        public static bool IsValidRole(string role)
        {
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Operator", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRole(string role)
        {
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Operator";
        }

        public static string Sha256Hex(string s)
        {
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(s ?? ""));
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
