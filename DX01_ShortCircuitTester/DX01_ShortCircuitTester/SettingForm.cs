using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using DX01_ShortCircuitTester.Services;

namespace DX01_ShortCircuitTester
{
    /// <summary>
    /// 參數設定對話框。讀取 / 編輯 AppSettings.Current；
    /// 套用 = 只更新記憶體；儲存 = 更新記憶體並寫入 Config\DX01Config.json。
    /// </summary>
    public partial class SettingForm : Form
    {
        // 1. 設備連線
        private TextBox txtLanIp, txtLanPort, txtVendorIdHex, txtProductIdHex;
        private ComboBox cbDebugLevel;
        // 2. 條碼規則管理（V2.4：多組規則，可新增 / 編輯 / 刪除 / 啟用停用）
        private CheckedListBox _clbRules;
        private System.Collections.Generic.List<BarcodeRule> _rules;
        // 3. 電阻
        private TextBox txtIRUpper, txtOLValue;
        // 4. 電壓
        private TextBox txtVoltUpper, txtVoltLower, txtVoltOn, txtVoltIsoUpper, txtDcVoltageRange;
        // 4b. V2.4 Power ON/OFF 自動偵測門檻
        private TextBox txtPowerOnThreshold, txtPowerOffThreshold, txtPowerPollIntervalMs, txtPowerWaitLogIntervalSec, txtPowerWaitTimeoutSec;
        // 5. 電流
        private TextBox txtCurrentMin, txtCurrentMax;
        // 6. Step 等待
        private readonly TextBox[] _stepBoxes = new TextBox[11];
        // 7. UI / 執行
        private TextBox txtPopupSeconds, txtStepFontSize, txtPollIntervalMs, txtReadTimeoutMs, txtRelaySwitchDelayMs;
        // 7b. V2.4 NG 重試（時間窗）
        private TextBox txtNgRetryTimeoutMs, txtNgRetryIntervalMs;
        // 8. V2.5 登入開關
        private CheckBox _chkEnableLogin;
        private bool _originalEnableLogin;
        // 9. V2.5 前站 Airflow PASS 檢查
        private CheckBox _chkEnableAirflow;
        private TextBox _txtAirflowUrl;

        private Button btnSave, btnCancel;

        private int _y;
        private Panel _body;

        private bool _saved;
        private string _originalSnapshot;

        private readonly bool _editable;

        public SettingForm() : this(true) { }

        /// <summary>editable=false 時（Operator）僅供檢視：所有輸入停用、隱藏「儲存」。</summary>
        public SettingForm(bool editable)
        {
            _editable = editable;
            InitializeComponent();
            BuildUi();
            LoadFromSettings();
            _originalSnapshot = BuildSnapshot();

            if (!_editable)
            {
                Text = "參數設定（檢視）";
                if (_body != null) _body.Enabled = false;   // 所有設定輸入反灰、不可改
                if (btnSave != null) { btnSave.Visible = false; btnSave.Enabled = false; }
            }
        }

        private void BuildUi()
        {
            _body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(4) };
            Controls.Add(_body);

            var pnlButtons = new Panel { Dock = DockStyle.Bottom, Height = 52 };
            Controls.Add(pnlButtons);

            btnCancel = new Button { Text = "取消", Size = new Size(100, 34), Location = new Point(470 - 112, 9), Anchor = AnchorStyles.Right };
            btnSave = new Button { Text = "儲存", Size = new Size(100, 34), Location = new Point(470 - 220, 9), Anchor = AnchorStyles.Right };
            btnCancel.Click += (s, e) => Close();
            btnSave.Click += BtnSave_Click;
            pnlButtons.Controls.Add(btnSave);
            pnlButtons.Controls.Add(btnCancel);

            _y = 10;

            Header("1. 設備連線");
            txtLanIp = TxtRow("LanIP");
            txtLanPort = TxtRow("LanPort");
            txtVendorIdHex = TxtRow("VendorIdHex");
            txtProductIdHex = TxtRow("ProductIdHex");
            cbDebugLevel = ComboRow("DebugLevel", new[] { "error", "info", "debug" });

            BuildBarcodeRulesSection();

            Header("3. 電阻條件");
            txtIRUpper = TxtRow("IRUpper(Ω)  → step3");
            txtOLValue = TxtRow("OLValue(Ω)  → step4/5");

            Header("4. 電壓條件");
            txtVoltUpper = TxtRow("VoltUpper(V)  → step8 max");
            txtVoltLower = TxtRow("VoltLower(V)  → step8 min");
            txtVoltOn = TxtRow("VoltOn(V)  → step7 min");
            txtVoltIsoUpper = TxtRow("VoltIsoUpper(V)  → step9/10");
            txtDcVoltageRange = TxtRow("DC Voltage Range(V)");

            Header("4b. Power 自動偵測門檻 (V2.4)");
            txtPowerOnThreshold = TxtRow("Power ON 門檻 (V)");
            txtPowerOffThreshold = TxtRow("Power OFF 門檻 (V)");
            txtPowerPollIntervalMs = TxtRow("Power 偵測間隔 (ms)");
            txtPowerWaitLogIntervalSec = TxtRow("Power 等待 Log 間隔 (s)");
            txtPowerWaitTimeoutSec = TxtRow("Power 等待逾時 (s)");

            Header("5. 電流條件 (保留)");
            txtCurrentMin = TxtRow("CurrentMin(A)");
            txtCurrentMax = TxtRow("CurrentMax(A)");

            Header("6. Step 等待時間 (ms，空白=0)");
            for (int i = 1; i <= 10; i++)
                _stepBoxes[i] = TxtRow("Step" + i + "WaitMs");

            Header("7. UI / 執行參數");
            txtPopupSeconds = TxtRow("PopupSeconds");
            txtStepFontSize = TxtRow("StepFontSize");
            txtPollIntervalMs = TxtRow("PollIntervalMs");
            txtReadTimeoutMs = TxtRow("ReadTimeoutMs");
            txtRelaySwitchDelayMs = TxtRow("RelaySwitchDelayMs");

            Header("7b. NG 重試時間窗 (V2.4)");
            txtNgRetryTimeoutMs = TxtRow("NG Retry Timeout (ms)");
            txtNgRetryIntervalMs = TxtRow("NG Retry Interval (ms)");

            Header("8. Login Authentication (Admin Only)");
            _chkEnableLogin = CheckRow("Enable Login Authentication");
            var loginHint = new Label
            {
                Text = "Unchecked = no login (auto Admin on startup). Changes take effect after restarting the application.",
                AutoSize = true,
                ForeColor = Color.Gray,
                Location = new Point(20, _y)
            };
            _body.Controls.Add(loginHint);
            _y += 28;

            Header("9. Airflow Previous Station Check (Admin Only)");
            _chkEnableAirflow = CheckRow("Enable Airflow Previous Station Check");
            _txtAirflowUrl = TxtRow("Airflow Previous Station API URL");
            _txtAirflowUrl.Width = 235;   // URL 較長，加寬輸入框
            var airflowHint = new Label
            {
                Text = "Called as {URL}?sn[]={barcode} before test start. Only PASS (non-empty, count != 0) allows testing.",
                AutoSize = true,
                ForeColor = Color.Gray,
                Location = new Point(20, _y)
            };
            _body.Controls.Add(airflowHint);
            _y += 28;
        }

        private CheckBox CheckRow(string label)
        {
            var c = new CheckBox { Text = label, AutoSize = true, Location = new Point(20, _y + 4) };
            _body.Controls.Add(c);
            _y += 32;
            return c;
        }

        private void Header(string text)
        {
            var l = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = Color.SteelBlue,
                Location = new Point(8, _y + 6)
            };
            _body.Controls.Add(l);
            _y += 34;
        }

        private TextBox TxtRow(string label)
        {
            var l = new Label { Text = label, AutoSize = true, Location = new Point(20, _y + 5) };
            var t = new TextBox { Location = new Point(230, _y), Width = 200 };
            _body.Controls.Add(l);
            _body.Controls.Add(t);
            _y += 32;
            return t;
        }

        // ===================== 條碼規則管理 (V2.4) =====================

        private void BuildBarcodeRulesSection()
        {
            Header("2. 條碼規則管理 (V2.4)");

            _clbRules = new CheckedListBox
            {
                Location = new Point(20, _y),
                Width = 410,
                Height = 120,
                CheckOnClick = true,
                IntegralHeight = false,
                Font = new Font("Consolas", 9F)
            };
            // 勾選 / 取消勾選 → 同步該規則的 Enabled
            _clbRules.ItemCheck += (s, e) =>
            {
                if (_rules != null && e.Index >= 0 && e.Index < _rules.Count)
                    _rules[e.Index].Enabled = (e.NewValue == CheckState.Checked);
            };
            _clbRules.DoubleClick += (s, e) => EditRule();
            _body.Controls.Add(_clbRules);
            _y += 126;

            var btnAdd = new Button { Text = "新增", Location = new Point(20, _y), Size = new Size(80, 30) };
            var btnEdit = new Button { Text = "編輯", Location = new Point(110, _y), Size = new Size(80, 30) };
            var btnDel = new Button { Text = "刪除", Location = new Point(200, _y), Size = new Size(80, 30) };
            btnAdd.Click += (s, e) => AddRule();
            btnEdit.Click += (s, e) => EditRule();
            btnDel.Click += (s, e) => DeleteRule();
            _body.Controls.Add(btnAdd);
            _body.Controls.Add(btnEdit);
            _body.Controls.Add(btnDel);
            _y += 40;
        }

        private void RefreshRuleList()
        {
            if (_clbRules == null) return;
            int sel = _clbRules.SelectedIndex;
            _clbRules.Items.Clear();
            if (_rules != null)
                foreach (var r in _rules)
                    _clbRules.Items.Add(RuleDisplay(r), r.Enabled);
            if (sel >= 0 && sel < _clbRules.Items.Count)
                _clbRules.SelectedIndex = sel;
        }

        private static string RuleDisplay(BarcodeRule r)
        {
            string name = string.IsNullOrEmpty(r.Name) ? "(未命名)" : r.Name;
            return name + "  |  " + r.Pattern;
        }

        private void AddRule()
        {
            using (var f = new BarcodeRuleEditForm(null))
            {
                if (f.ShowDialog(this) == DialogResult.OK && f.Result != null)
                {
                    _rules.Add(f.Result);
                    RefreshRuleList();
                    _clbRules.SelectedIndex = _rules.Count - 1;
                }
            }
        }

        private void EditRule()
        {
            int i = _clbRules.SelectedIndex;
            if (_rules == null || i < 0 || i >= _rules.Count) return;
            using (var f = new BarcodeRuleEditForm(_rules[i]))
            {
                if (f.ShowDialog(this) == DialogResult.OK && f.Result != null)
                {
                    _rules[i] = f.Result;
                    RefreshRuleList();
                    _clbRules.SelectedIndex = i;
                }
            }
        }

        private void DeleteRule()
        {
            int i = _clbRules.SelectedIndex;
            if (_rules == null || i < 0 || i >= _rules.Count) return;
            _rules.RemoveAt(i);
            RefreshRuleList();
        }

        private ComboBox ComboRow(string label, string[] items)
        {
            var l = new Label { Text = label, AutoSize = true, Location = new Point(20, _y + 5) };
            var c = new ComboBox { Location = new Point(230, _y), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            c.Items.AddRange(items);
            _body.Controls.Add(l);
            _body.Controls.Add(c);
            _y += 32;
            return c;
        }

        private void LoadFromSettings()
        {
            var c = AppSettings.Current;
            txtLanIp.Text = c.Ip;
            txtLanPort.Text = c.TcpPort.ToString(CultureInfo.InvariantCulture);
            txtVendorIdHex.Text = c.VendorIdHex;
            txtProductIdHex.Text = c.ProductIdHex;
            cbDebugLevel.SelectedItem = c.DebugLevel;
            if (cbDebugLevel.SelectedIndex < 0) cbDebugLevel.SelectedItem = "debug";

            _rules = new System.Collections.Generic.List<BarcodeRule>();
            if (c.BarcodeRules != null)
                foreach (var r in c.BarcodeRules)
                    if (r != null) _rules.Add(r.Clone());
            RefreshRuleList();

            txtIRUpper.Text = Dbl(c.Step3CaseToChassisMax);
            txtOLValue.Text = Dbl(c.Step4PPlusInsulationMin);

            txtVoltUpper.Text = Dbl(c.Step8PPlusMinusMax);
            txtVoltLower.Text = Dbl(c.Step8PPlusMinusMin);
            txtVoltOn.Text = Dbl(c.Step7TotalVoltageMin);
            txtVoltIsoUpper.Text = Dbl(c.Step9PPlusToCaseMax);
            txtDcVoltageRange.Text = Dbl(c.DcVoltageRange);

            txtPowerOnThreshold.Text = Dbl(c.PowerOnThreshold);
            txtPowerOffThreshold.Text = Dbl(c.PowerOffThreshold);
            txtPowerPollIntervalMs.Text = c.PowerPollIntervalMs.ToString(CultureInfo.InvariantCulture);
            txtPowerWaitLogIntervalSec.Text = c.PowerWaitLogIntervalSec.ToString(CultureInfo.InvariantCulture);
            txtPowerWaitTimeoutSec.Text = c.PowerWaitTimeoutSec.ToString(CultureInfo.InvariantCulture);

            txtCurrentMin.Text = Dbl(c.CurrentMin);
            txtCurrentMax.Text = Dbl(c.CurrentMax);

            for (int i = 1; i <= 10; i++)
                _stepBoxes[i].Text = c.StepWaitMs[i].ToString(CultureInfo.InvariantCulture);

            txtPopupSeconds.Text = c.PopupSeconds.ToString(CultureInfo.InvariantCulture);
            txtStepFontSize.Text = c.StepFontSize.ToString(CultureInfo.InvariantCulture);
            txtPollIntervalMs.Text = c.PollIntervalMs.ToString(CultureInfo.InvariantCulture);
            txtReadTimeoutMs.Text = c.ReadTimeoutMs.ToString(CultureInfo.InvariantCulture);
            txtRelaySwitchDelayMs.Text = c.RelaySwitchDelayMs.ToString(CultureInfo.InvariantCulture);
            txtNgRetryTimeoutMs.Text = c.NgRetryTimeoutMs.ToString(CultureInfo.InvariantCulture);
            txtNgRetryIntervalMs.Text = c.NgRetryIntervalMs.ToString(CultureInfo.InvariantCulture);

            _chkEnableLogin.Checked = c.EnableLogin;
            _originalEnableLogin = c.EnableLogin;

            _chkEnableAirflow.Checked = c.EnableAirflowPreviousStationCheck;
            _txtAirflowUrl.Text = c.AirflowPreviousStationApiUrl;
        }

        private static string Dbl(double v)
        {
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string err;
            if (!TryCollect(out err))
            {
                MsgBox.Show(this, "設定錯誤", err, MessageBoxIcon.Warning, "確定");
                return;
            }

            string saveErr;
            if (!AppSettings.Current.Save(out saveErr))
            {
                MsgBox.Show(this, "錯誤", "儲存失敗:\n" + saveErr, MessageBoxIcon.Error, "確定");
                return;
            }

            string savedMsg = "設定已儲存";
            if (_chkEnableLogin != null && _chkEnableLogin.Checked != _originalEnableLogin)
                savedMsg += "\n\nLogin authentication setting changed.\nPlease restart the application to take effect.";
            MsgBox.Show(this, "參數設定", savedMsg, MessageBoxIcon.Information, "確定");
            _saved = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>取消 / 關閉時，若有未儲存修改則詢問是否放棄。</summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_saved && BuildSnapshot() != _originalSnapshot)
            {
                // 1 = 是（放棄）；0 = 否 / 關閉視窗
                int r = MsgBox.Show(this, "放棄修改", "尚有未儲存的修改，確定放棄?", MessageBoxIcon.Warning, "否", "是");
                if (r != 1)
                {
                    e.Cancel = true;
                    return;
                }
                DialogResult = DialogResult.Cancel;
            }
            base.OnFormClosing(e);
        }

        /// <summary>目前所有欄位值的快照，用於偵測是否有未儲存修改。</summary>
        private string BuildSnapshot()
        {
            var sb = new StringBuilder();
            sb.Append(txtLanIp.Text).Append('|').Append(txtLanPort.Text).Append('|')
              .Append(txtVendorIdHex.Text).Append('|').Append(txtProductIdHex.Text).Append('|')
              .Append(cbDebugLevel.SelectedItem).Append('|').Append(RulesSnapshot()).Append('|')
              .Append(txtIRUpper.Text).Append('|').Append(txtOLValue.Text).Append('|')
              .Append(txtVoltUpper.Text).Append('|').Append(txtVoltLower.Text).Append('|')
              .Append(txtVoltOn.Text).Append('|').Append(txtVoltIsoUpper.Text).Append('|').Append(txtDcVoltageRange.Text).Append('|')
              .Append(txtPowerOnThreshold.Text).Append('|').Append(txtPowerOffThreshold.Text).Append('|').Append(txtPowerPollIntervalMs.Text).Append('|')
              .Append(txtPowerWaitLogIntervalSec.Text).Append('|').Append(txtPowerWaitTimeoutSec.Text).Append('|')
              .Append(txtCurrentMin.Text).Append('|').Append(txtCurrentMax.Text).Append('|');
            for (int i = 1; i <= 10; i++)
                sb.Append(_stepBoxes[i].Text).Append('|');
            sb.Append(txtPopupSeconds.Text).Append('|').Append(txtStepFontSize.Text).Append('|')
              .Append(txtPollIntervalMs.Text).Append('|').Append(txtReadTimeoutMs.Text).Append('|')
              .Append(txtRelaySwitchDelayMs.Text).Append('|')
              .Append(txtNgRetryTimeoutMs.Text).Append('|').Append(txtNgRetryIntervalMs.Text).Append('|')
              .Append(_chkEnableLogin.Checked).Append('|')
              .Append(_chkEnableAirflow.Checked).Append('|').Append(_txtAirflowUrl.Text);
            return sb.ToString();
        }

        /// <summary>條碼規則的快照字串（供偵測未儲存修改）。</summary>
        private string RulesSnapshot()
        {
            if (_rules == null) return "";
            var sb = new StringBuilder();
            foreach (var r in _rules)
                sb.Append(r.Name).Append('~').Append(r.Pattern).Append('~').Append(r.Enabled).Append(';');
            return sb.ToString();
        }

        /// <summary>驗證所有欄位並寫入 AppSettings.Current（全部通過才寫入）。</summary>
        private bool TryCollect(out string err)
        {
            err = null;
            double irUpper, olValue, voltUpper, voltLower, voltOn, voltIso, curMin, curMax;
            if (!PD(txtIRUpper, "IRUpper", out irUpper, ref err)) return false;
            if (!PD(txtOLValue, "OLValue", out olValue, ref err)) return false;
            if (!PD(txtVoltUpper, "VoltUpper", out voltUpper, ref err)) return false;
            if (!PD(txtVoltLower, "VoltLower", out voltLower, ref err)) return false;
            if (!PD(txtVoltOn, "VoltOn", out voltOn, ref err)) return false;
            if (!PD(txtVoltIsoUpper, "VoltIsoUpper", out voltIso, ref err)) return false;
            double dcRange;
            if (!PD(txtDcVoltageRange, "DC Voltage Range", out dcRange, ref err)) return false;
            double powerOn, powerOff;
            if (!PD(txtPowerOnThreshold, "Power ON 門檻", out powerOn, ref err)) return false;
            if (!PD(txtPowerOffThreshold, "Power OFF 門檻", out powerOff, ref err)) return false;
            if (powerOff >= powerOn) { err = "Power OFF 門檻必須小於 Power ON 門檻。"; return false; }
            if (!PD(txtCurrentMin, "CurrentMin", out curMin, ref err)) return false;
            if (!PD(txtCurrentMax, "CurrentMax", out curMax, ref err)) return false;

            int lanPort;
            if (!PI(txtLanPort, "LanPort", out lanPort, ref err)) return false;
            if (lanPort < 1 || lanPort > 65535) { err = "LanPort 必須是 1~65535。"; return false; }

            int vid, pid;
            if (!PHex(txtVendorIdHex, "VendorIdHex", out vid, ref err)) return false;
            if (!PHex(txtProductIdHex, "ProductIdHex", out pid, ref err)) return false;

            var waits = new int[11];
            for (int i = 1; i <= 10; i++)
                if (!PWait(_stepBoxes[i], "Step" + i + "WaitMs", out waits[i], ref err)) return false;

            int powerPoll;
            if (!PI(txtPowerPollIntervalMs, "Power 偵測間隔", out powerPoll, ref err)) return false;
            if (powerPoll < 0) { err = "Power 偵測間隔不可為負數。"; return false; }
            int powerWaitLog;
            if (!PI(txtPowerWaitLogIntervalSec, "Power 等待 Log 間隔", out powerWaitLog, ref err)) return false;
            if (powerWaitLog < 1) { err = "Power 等待 Log 間隔必須 >= 1 秒。"; return false; }
            int powerWaitTimeout;
            if (!PI(txtPowerWaitTimeoutSec, "Power 等待逾時", out powerWaitTimeout, ref err)) return false;
            if (powerWaitTimeout < 0) { err = "Power 等待逾時不可為負數（0 = 無限等待）。"; return false; }

            int popup, font, poll, readTo, relayDelay;
            if (!PI(txtPopupSeconds, "PopupSeconds", out popup, ref err)) return false;
            if (!PI(txtStepFontSize, "StepFontSize", out font, ref err)) return false;
            if (!PI(txtPollIntervalMs, "PollIntervalMs", out poll, ref err)) return false;
            if (!PI(txtReadTimeoutMs, "ReadTimeoutMs", out readTo, ref err)) return false;
            if (!PI(txtRelaySwitchDelayMs, "RelaySwitchDelayMs", out relayDelay, ref err)) return false;

            int ngTimeout, ngInterval;
            if (!PI(txtNgRetryTimeoutMs, "NG Retry Timeout", out ngTimeout, ref err)) return false;
            if (ngTimeout < 0) { err = "NG Retry Timeout 不可為負數。"; return false; }
            if (!PI(txtNgRetryIntervalMs, "NG Retry Interval", out ngInterval, ref err)) return false;
            if (ngInterval <= 0) { err = "NG Retry Interval 必須大於 0。"; return false; }

            if (txtLanIp.Text.Trim().Length == 0) { err = "LanIP 不可空白。"; return false; }

            if (_chkEnableAirflow.Checked && _txtAirflowUrl.Text.Trim().Length == 0)
            { err = "Airflow Previous Station API URL 不可空白（已啟用前站檢查）。"; return false; }

            var c = AppSettings.Current;
            c.Ip = txtLanIp.Text.Trim();
            c.TcpPort = lanPort;
            c.VendorId = vid;
            c.ProductId = pid;
            c.DebugLevel = cbDebugLevel.SelectedItem != null ? cbDebugLevel.SelectedItem.ToString() : "debug";
            c.EnableLogin = _chkEnableLogin.Checked;
            c.EnableAirflowPreviousStationCheck = _chkEnableAirflow.Checked;
            c.AirflowPreviousStationApiUrl = _txtAirflowUrl.Text.Trim();
            c.BarcodeRules = new System.Collections.Generic.List<BarcodeRule>();
            if (_rules != null)
                foreach (var r in _rules)
                    if (r != null) c.BarcodeRules.Add(r.Clone());

            c.Step3CaseToChassisMax = irUpper;
            c.Step4PPlusInsulationMin = olValue;
            c.Step5PMinusInsulationMin = olValue;
            c.Step7TotalVoltageMin = voltOn;
            c.Step8PPlusMinusMin = voltLower;
            c.Step8PPlusMinusMax = voltUpper;
            c.Step9PPlusToCaseMax = voltIso;
            c.Step10PMinusToCaseMax = voltIso;
            c.DcVoltageRange = dcRange;

            c.PowerOnThreshold = powerOn;
            c.PowerOffThreshold = powerOff;
            c.PowerPollIntervalMs = powerPoll;
            c.PowerWaitLogIntervalSec = powerWaitLog;
            c.PowerWaitTimeoutSec = powerWaitTimeout;

            c.CurrentMin = curMin;
            c.CurrentMax = curMax;

            for (int i = 1; i <= 10; i++)
                c.StepWaitMs[i] = waits[i];

            c.PopupSeconds = popup;
            c.StepFontSize = font;
            c.PollIntervalMs = poll;
            c.ReadTimeoutMs = readTo;
            c.RelaySwitchDelayMs = relayDelay;
            c.NgRetryTimeoutMs = ngTimeout;
            c.NgRetryIntervalMs = ngInterval;
            return true;
        }

        private static bool PD(TextBox t, string name, out double v, ref string err)
        {
            if (double.TryParse(t.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return true;
            err = name + " 必須是數字。";
            return false;
        }

        private static bool PI(TextBox t, string name, out int v, ref string err)
        {
            if (int.TryParse(t.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                return true;
            err = name + " 必須是整數。";
            return false;
        }

        private static bool PWait(TextBox t, string name, out int v, ref string err)
        {
            v = 0;
            string s = t.Text.Trim();
            if (s.Length == 0) { v = 0; return true; } // 空白 = 0
            if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
            {
                err = name + " 必須是整數或空白。";
                return false;
            }
            if (v < 0) { err = name + " 不可為負數。"; return false; }
            return true;
        }

        private static bool PHex(TextBox t, string name, out int v, ref string err)
        {
            v = 0;
            string s = t.Text.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);
            if (int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v))
                return true;
            err = name + " 必須是 16 進位 (例如 0x16C0)。";
            return false;
        }
    }
}
