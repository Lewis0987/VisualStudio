using System;
using System.Drawing;
using System.Windows.Forms;
using DX01_ShortCircuitTester.Services;

namespace DX01_ShortCircuitTester
{
    /// <summary>
    /// 員工登入視窗：工號（8 碼數字）+ 密碼（遮罩）。
    /// 驗證交由 <see cref="OperatorAuth.TryLogin"/>；成功回 DialogResult.OK（狀態已寫入 auth）。
    /// 密碼僅用於比對，不顯示、不記錄。
    /// </summary>
    public sealed class LoginForm : Form
    {
        private readonly OperatorAuth _auth;
        private TextBox _txtId;
        private TextBox _txtPwd;

        public LoginForm(OperatorAuth auth)
        {
            _auth = auth;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "權限驗證";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            Font = new Font("Microsoft JhengHei UI", 9.5F);
            ClientSize = new Size(300, 188);

            var lblTitle = new Label { Text = "請輸入工號 / 密碼：", Location = new Point(20, 14), AutoSize = true };

            var lblId = new Label { Text = "工號：", Location = new Point(24, 50), AutoSize = true };
            // 不限制輸入長度（移除 MaxLength）；格式驗證於登入時進行
            _txtId = new TextBox { Location = new Point(96, 46), Size = new Size(176, 25) };
            // Enter：工號輸入完成 → 跳到密碼欄位（不直接送出，避免略過密碼）
            _txtId.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _txtPwd.Focus();
                }
            };

            var lblPwd = new Label { Text = "密碼：", Location = new Point(24, 88), AutoSize = true };
            // Enter：密碼輸入完成 → 等同按下「確定」（由 AcceptButton = btnOk 處理）
            _txtPwd = new TextBox { Location = new Point(96, 84), Size = new Size(176, 25), UseSystemPasswordChar = true };

            var btnOk = new Button { Text = "確定", Size = new Size(82, 30), Location = new Point(96, 132) };
            var btnCancel = new Button { Text = "取消", Size = new Size(82, 30), Location = new Point(190, 132), DialogResult = DialogResult.Cancel };
            btnOk.Click += BtnLogin_Click;

            Controls.Add(lblTitle);
            Controls.Add(lblId);
            Controls.Add(_txtId);
            Controls.Add(lblPwd);
            Controls.Add(_txtPwd);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            string err;
            if (_auth.TryLogin(_txtId.Text, _txtPwd.Text, out err))
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            // 登入失敗統一訊息：不區分工號格式錯 / 工號不存在 / 密碼錯誤，避免透露帳號是否存在
            MsgBox.Show(this, "登入失敗", "工號或密碼有錯誤\n請重新輸入工號", MessageBoxIcon.Warning, "確定");
            _txtPwd.Clear();
            _txtId.Focus();
            _txtId.SelectAll();
        }
    }
}
