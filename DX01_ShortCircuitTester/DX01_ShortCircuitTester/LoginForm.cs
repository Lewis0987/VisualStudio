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

            var lblTitle = new Label { Text = "請輸入帳號 / 密碼：", Location = new Point(20, 14), AutoSize = true };

            var lblId = new Label { Text = "帳號：", Location = new Point(24, 50), AutoSize = true };
            _txtId = new TextBox { Location = new Point(96, 46), Size = new Size(176, 25), MaxLength = 8 };

            var lblPwd = new Label { Text = "密碼：", Location = new Point(24, 88), AutoSize = true };
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

            MsgBox.Show(this, "登入失敗", err, MessageBoxIcon.Warning, "確定");
            _txtPwd.Clear();
            if (err.Contains("密碼"))
            {
                _txtPwd.Focus();
            }
            else
            {
                _txtId.Focus();
                _txtId.SelectAll();
            }
        }
    }
}
