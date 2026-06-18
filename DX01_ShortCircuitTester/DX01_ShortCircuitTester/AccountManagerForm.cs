using System;
using System.Drawing;
using System.Windows.Forms;
using DX01_ShortCircuitTester.Services;

namespace DX01_ShortCircuitTester
{
    /// <summary>
    /// Admin 專用帳號管理：新增 / 刪除 Operator、重設密碼。
    /// 密碼自動以 SHA256 儲存、不顯示明文；預設 Admin(00000000) 不可刪除。
    /// 變更即寫回 Config\Operators.json；事件寫入 Debug Log（不含密碼）。
    /// </summary>
    public sealed class AccountManagerForm : Form
    {
        private readonly OperatorAuth _auth;
        private readonly Action<string> _log;   // Debug Log 回呼（不記密碼）

        private DataGridView _grid;
        private TextBox _txtId;
        private ComboBox _cbRole;
        private TextBox _txtPwd;
        private TextBox _txtPwd2;

        public AccountManagerForm(OperatorAuth auth, Action<string> log)
        {
            _auth = auth;
            _log = log;
            BuildUi();
            ReloadGrid();
        }

        private void BuildUi()
        {
            Text = "帳號管理";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            Font = new Font("Microsoft JhengHei UI", 9.5F);
            ClientSize = new Size(520, 360);

            _grid = new DataGridView
            {
                Location = new Point(12, 12),
                Size = new Size(496, 180),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colId", HeaderText = "工號" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRole", HeaderText = "角色" });
            Controls.Add(_grid);

            // 輸入區（新增 / 重設密碼共用）
            var gb = new GroupBox { Text = "新增 / 重設", Location = new Point(12, 200), Size = new Size(496, 110), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            var lblId = new Label { Text = "工號：", Location = new Point(14, 28), AutoSize = true };
            // 不限制輸入長度（移除 MaxLength）；格式驗證於新增時進行
            _txtId = new TextBox { Location = new Point(70, 24), Size = new Size(120, 25) };
            var lblRole = new Label { Text = "角色：", Location = new Point(206, 28), AutoSize = true };
            _cbRole = new ComboBox { Location = new Point(262, 24), Size = new Size(110, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            _cbRole.Items.AddRange(new object[] { "Operator", "Admin" });
            _cbRole.SelectedIndex = 0;

            var lblPwd = new Label { Text = "密碼：", Location = new Point(14, 66), AutoSize = true };
            _txtPwd = new TextBox { Location = new Point(70, 62), Size = new Size(120, 25), UseSystemPasswordChar = true };
            var lblPwd2 = new Label { Text = "確認：", Location = new Point(206, 66), AutoSize = true };
            _txtPwd2 = new TextBox { Location = new Point(262, 62), Size = new Size(110, 25), UseSystemPasswordChar = true };

            gb.Controls.Add(lblId); gb.Controls.Add(_txtId);
            gb.Controls.Add(lblRole); gb.Controls.Add(_cbRole);
            gb.Controls.Add(lblPwd); gb.Controls.Add(_txtPwd);
            gb.Controls.Add(lblPwd2); gb.Controls.Add(_txtPwd2);
            Controls.Add(gb);

            var btnAdd = new Button { Text = "新增帳號", Location = new Point(388, 22), Size = new Size(96, 28), Parent = gb };
            var btnReset = new Button { Text = "重設選取密碼", Location = new Point(388, 60), Size = new Size(96, 28), Parent = gb };
            btnAdd.Click += BtnAdd_Click;
            btnReset.Click += BtnReset_Click;

            var btnDelete = new Button { Text = "刪除選取", Location = new Point(12, 322), Size = new Size(110, 28), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            var btnClose = new Button { Text = "關閉", Location = new Point(420, 322), Size = new Size(88, 28), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.OK };
            btnDelete.Click += BtnDelete_Click;
            Controls.Add(btnDelete);
            Controls.Add(btnClose);

            // 選取清單列「不」自動帶入工號 / 角色，避免新增帳號時被誤改。
            // 刪除 / 重設密碼以清單選取列（SelectedId）為對象，與輸入框分離。

            AcceptButton = btnClose;
            CancelButton = btnClose;
        }

        private void ReloadGrid()
        {
            _grid.Rows.Clear();
            foreach (OperatorAccount a in _auth.ListAccounts())
                _grid.Rows.Add(a.Id, a.Role);
            _grid.ClearSelection();   // 預設不選取任何帳號，需使用者明確點選
        }

        private string SelectedId()
        {
            if (_grid.SelectedRows.Count == 0) return null;
            object v = _grid.SelectedRows[0].Cells["colId"].Value;
            return v == null ? null : v.ToString();
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            // 工號格式驗證（英數字工號）
            if (!OperatorAuth.IsValidId(_txtId.Text))
            {
                MsgBox.Show(this, "新增失敗", "工號格式錯誤\n請輸入英數字工號", MessageBoxIcon.Warning, "確定");
                return;
            }

            if (_txtPwd.Text != _txtPwd2.Text)
            {
                MsgBox.Show(this, "新增失敗", "兩次密碼輸入不一致。", MessageBoxIcon.Warning, "確定");
                return;
            }

            // 密碼格式驗證（英數字密碼）
            if (!OperatorAuth.IsValidPassword(_txtPwd.Text))
            {
                MsgBox.Show(this, "新增失敗", "密碼格式錯誤\n請輸入英數字密碼", MessageBoxIcon.Warning, "確定");
                return;
            }

            string err;
            if (!_auth.AddAccount(_txtId.Text, _cbRole.Text, _txtPwd.Text, out err))
            {
                MsgBox.Show(this, "新增失敗", err, MessageBoxIcon.Warning, "確定");
                return;
            }

            LogEvent("[Account Added]", _txtId.Text.Trim() + " (" + _cbRole.Text + ")");
            ClearInputs();
            ReloadGrid();
            MsgBox.Show(this, "帳號管理", "帳號已新增。", MessageBoxIcon.Information, "確定");
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            string id = SelectedId();
            if (id == null)
            {
                MsgBox.Show(this, "重設密碼", "請先選取帳號。", MessageBoxIcon.Warning, "確定");
                return;
            }
            if (_txtPwd.Text != _txtPwd2.Text)
            {
                MsgBox.Show(this, "重設失敗", "兩次密碼輸入不一致。", MessageBoxIcon.Warning, "確定");
                return;
            }

            string err;
            if (!_auth.ResetPassword(id, _txtPwd.Text, out err))
            {
                MsgBox.Show(this, "重設失敗", err, MessageBoxIcon.Warning, "確定");
                return;
            }

            LogEvent("[Password Reset]", id);   // 僅記工號，不記密碼
            _txtPwd.Clear();
            _txtPwd2.Clear();
            MsgBox.Show(this, "帳號管理", "密碼已重設。", MessageBoxIcon.Information, "確定");
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            string id = SelectedId();
            if (id == null)
            {
                MsgBox.Show(this, "刪除帳號", "請先在清單選取要刪除的帳號。", MessageBoxIcon.Warning, "確定");
                return;
            }

            // 1 = 確定刪除
            if (MsgBox.Show(this, "刪除帳號", "確定刪除工號 " + id + " ？", MessageBoxIcon.Warning, "取消", "刪除") != 1)
                return;

            string err;
            if (!_auth.DeleteAccount(id, out err))
            {
                MsgBox.Show(this, "刪除失敗", err, MessageBoxIcon.Warning, "確定");
                return;
            }

            LogEvent("[Account Deleted]", id);
            ClearInputs();
            ReloadGrid();
        }

        private void ClearInputs()
        {
            _txtId.Clear();
            _txtPwd.Clear();
            _txtPwd2.Clear();
            _cbRole.SelectedIndex = 0;
        }

        private void Log(string msg)
        {
            try { _log?.Invoke(msg); } catch { }
        }

        /// <summary>帳號管理事件 Log：標題 + 操作者(Admin) + 目標工號；絕不記錄密碼。</summary>
        private void LogEvent(string header, string target)
        {
            Log(header);
            Log("Admin : " + _auth.OperatorId);
            Log("Target : " + target);
        }
    }
}
