using System;
using System.Drawing;
using System.Windows.Forms;

namespace DX01_ShortCircuitTester
{
    /// <summary>
    /// 全專案統一的訊息彈窗（標題列 + 左側 icon + 換行訊息 + 下方灰色按鈕區）。
    /// 取代各處大小 / 樣式不一的 MessageBox 與自訂彈窗：
    ///   單一按鈕 → 置中；兩個（含以上）按鈕 → 等寬、水平排列、置中、間距固定。
    /// 回傳被點擊按鈕的索引（0-based）；視窗關閉(X)回傳 -1。
    /// </summary>
    internal static class MsgBox
    {
        private const int Pad = 20;          // 內容左右 / 上邊距
        private const int IconSize = 32;
        private const int IconGap = 14;      // icon 與文字間距
        private const int MaxTextWidth = 320;
        private const int BtnH = 28;
        private const int BtnGap = 12;
        private const int BtnAreaH = 56;     // 灰色按鈕區高度
        private const int MinClientW = 230;

        public static int Show(IWin32Window owner, string title, string message,
            MessageBoxIcon icon = MessageBoxIcon.None, params string[] buttons)
        {
            if (buttons == null || buttons.Length == 0)
                buttons = new[] { "確定" };

            using (var dlg = new Form())
            {
                dlg.Text = title ?? "";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MinimizeBox = false;
                dlg.MaximizeBox = false;
                dlg.ShowIcon = false;
                dlg.ShowInTaskbar = false;
                dlg.Font = SystemFonts.MessageBoxFont ?? Control.DefaultFont;

                // 內容容器（上方），灰色按鈕區（下方 Dock）
                Icon sysIcon = SystemIconFor(icon);
                int textLeft = sysIcon != null ? Pad + IconSize + IconGap : Pad;

                var lbl = new Label
                {
                    AutoSize = true,
                    MaximumSize = new Size(MaxTextWidth, 0),
                    Location = new Point(textLeft, Pad),
                    Text = message ?? ""
                };
                dlg.Controls.Add(lbl);

                PictureBox pic = null;
                if (sysIcon != null)
                {
                    pic = new PictureBox
                    {
                        Image = sysIcon.ToBitmap(),
                        Size = new Size(IconSize, IconSize),
                        Location = new Point(Pad, Pad),
                        SizeMode = PictureBoxSizeMode.StretchImage
                    };
                    dlg.Controls.Add(pic);

                    // icon 與文字垂直置中對齊（短文字置中於 icon；長文字 icon 置中於文字）
                    if (lbl.Height < IconSize)
                        lbl.Top = Pad + (IconSize - lbl.Height) / 2;
                    else
                        pic.Top = Pad + (lbl.Height - IconSize) / 2;
                }

                int contentBottom = Math.Max(lbl.Bottom, sysIcon != null ? pic.Bottom : 0);

                // 按鈕等寬（取最長文字 + 內距，最小 84）
                int btnW = 84;
                foreach (var t in buttons)
                {
                    int w = TextRenderer.MeasureText(t, dlg.Font).Width + 28;
                    if (w > btnW) btnW = w;
                }
                int rowW = buttons.Length * btnW + (buttons.Length - 1) * BtnGap;

                int clientW = Math.Max(MinClientW, textLeft + lbl.Width + Pad);
                if (clientW < rowW + Pad * 2)
                    clientW = rowW + Pad * 2;

                dlg.ClientSize = new Size(clientW, contentBottom + Pad + BtnAreaH);

                var area = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = BtnAreaH,
                    BackColor = SystemColors.Control
                };
                dlg.Controls.Add(area);

                int startX = (clientW - rowW) / 2;   // 按鈕群置中
                int btnY = (BtnAreaH - BtnH) / 2;
                Button lastBtn = null, firstBtn = null;
                for (int i = 0; i < buttons.Length; i++)
                {
                    int idx = i;
                    var b = new Button
                    {
                        Text = buttons[i],
                        Size = new Size(btnW, BtnH),
                        Location = new Point(startX + i * (btnW + BtnGap), btnY)
                    };
                    b.Click += (s, e) => { dlg.Tag = idx; dlg.DialogResult = DialogResult.OK; };
                    area.Controls.Add(b);
                    if (i == 0) firstBtn = b;
                    lastBtn = b;
                }

                dlg.AcceptButton = lastBtn;                 // Enter = 最右（確定 / 主要）
                if (buttons.Length == 1)
                    dlg.CancelButton = lastBtn;             // 單鍵時 Esc 亦可關閉

                dlg.Tag = -1;
                dlg.ShowDialog(owner);
                return dlg.Tag is int ? (int)dlg.Tag : -1;
            }
        }

        /// <summary>單一「確定」按鈕的簡便呼叫。</summary>
        public static void Info(IWin32Window owner, string title, string message, MessageBoxIcon icon = MessageBoxIcon.Information)
        {
            Show(owner, title, message, icon, "確定");
        }

        private static Icon SystemIconFor(MessageBoxIcon icon)
        {
            switch (icon)
            {
                case MessageBoxIcon.Error: return SystemIcons.Error;       // = Hand / Stop
                case MessageBoxIcon.Warning: return SystemIcons.Warning;   // = Exclamation
                case MessageBoxIcon.Information: return SystemIcons.Information; // = Asterisk
                case MessageBoxIcon.Question: return SystemIcons.Question;
                default: return null;
            }
        }
    }
}
