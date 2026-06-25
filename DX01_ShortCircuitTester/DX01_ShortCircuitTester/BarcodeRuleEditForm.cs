using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DX01_ShortCircuitTester.Services;

namespace DX01_ShortCircuitTester
{
    /// <summary>
    /// 條碼規則 新增 / 編輯 對話框。
    /// 儲存前先驗證 Regex 是否合法；不合法時欄位下方紅字「Regex format error.」且不可儲存（不跳 MessageBox）。
    /// </summary>
    public sealed class BarcodeRuleEditForm : Form
    {
        private readonly TextBox _txtName;
        private readonly TextBox _txtPattern;
        private readonly CheckBox _chkEnabled;
        private readonly Label _lblError;

        /// <summary>儲存後的規則（DialogResult.OK 時有效）。</summary>
        public BarcodeRule Result { get; private set; }

        public BarcodeRuleEditForm(BarcodeRule existing)
        {
            Text = existing == null ? "新增條碼規則" : "編輯條碼規則";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(470, 236);
            Font = new Font("Microsoft JhengHei UI", 10F);

            var lblName = new Label { Text = "規則名稱：", AutoSize = true, Location = new Point(16, 16) };
            _txtName = new TextBox { Location = new Point(16, 42), Width = 436 };

            var lblPat = new Label { Text = "條碼格式 (Regex)：", AutoSize = true, Location = new Point(16, 78) };
            _txtPattern = new TextBox { Location = new Point(16, 104), Width = 436, Font = new Font("Consolas", 10F) };

            _lblError = new Label { AutoSize = true, ForeColor = Color.Red, Location = new Point(16, 134), Text = "" };

            _chkEnabled = new CheckBox { Text = "啟用", AutoSize = true, Location = new Point(16, 162), Checked = true };

            var btnOk = new Button { Text = "確定", Size = new Size(96, 32), Location = new Point(256, 192) };
            var btnCancel = new Button { Text = "取消", Size = new Size(96, 32), Location = new Point(356, 192), DialogResult = DialogResult.Cancel };
            btnOk.Click += BtnOk_Click;

            if (existing != null)
            {
                _txtName.Text = existing.Name;
                _txtPattern.Text = existing.Pattern;
                _chkEnabled.Checked = existing.Enabled;
            }

            // 輸入變動 → 清除紅字
            _txtPattern.TextChanged += (s, e) => _lblError.Text = "";

            Controls.AddRange(new Control[]
            {
                lblName, _txtName, lblPat, _txtPattern, _lblError, _chkEnabled, btnOk, btnCancel
            });
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            string pattern = _txtPattern.Text.Trim();

            // 先驗證 Regex 是否合法（空白或語法錯誤 → 不可儲存，紅字提示，不跳 MessageBox）
            if (pattern.Length == 0)
            {
                _lblError.Text = "Regex format error.";
                _txtPattern.Focus();
                return;
            }
            try { Regex.Match("", pattern); }
            catch
            {
                _lblError.Text = "Regex format error.";
                _txtPattern.Focus();
                return;
            }

            string name = _txtName.Text.Trim();
            if (name.Length == 0) name = pattern;   // 未命名 → 以格式字串當名稱

            Result = new BarcodeRule(name, pattern, _chkEnabled.Checked);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
