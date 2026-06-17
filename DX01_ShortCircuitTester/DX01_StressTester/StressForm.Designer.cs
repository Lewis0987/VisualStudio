namespace DX01_StressTester
{
    partial class StressForm
    {
        private System.ComponentModel.IContainer components = null;

        // Tabs
        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage tabRun;
        private System.Windows.Forms.TabPage tabSteps;

        // 設備狀態
        private System.Windows.Forms.GroupBox gbDevice;
        private System.Windows.Forms.Label lblGdm;
        private System.Windows.Forms.Label lblRelay;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnDisconnect;

        // 壓力測試設定
        private System.Windows.Forms.GroupBox gbConfig;
        private System.Windows.Forms.Label lblLoop;
        private System.Windows.Forms.ComboBox cbLoopCount;
        private System.Windows.Forms.Label lblNgActionCap;
        private System.Windows.Forms.ComboBox cbNgAction;

        // 測試控制
        private System.Windows.Forms.GroupBox gbControl;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;

        // 統計資訊
        private System.Windows.Forms.GroupBox gbStat;
        private System.Windows.Forms.Label lblCurrentLoop;
        private System.Windows.Forms.Label lblPass;
        private System.Windows.Forms.Label lblFail;
        private System.Windows.Forms.Label lblElapsed;
        private System.Windows.Forms.Label lblNgAction;

        // Log
        private System.Windows.Forms.GroupBox gbLog;
        private System.Windows.Forms.RichTextBox txtLog;

        // Step 設定
        private System.Windows.Forms.Label lblStepHint;
        private System.Windows.Forms.DataGridView dgvSteps;
        private System.Windows.Forms.Button btnReloadCfg;
        private System.Windows.Forms.Button btnSaveCfg;
        private System.Windows.Forms.Button btnDefaultCfg;

        // Grid 欄位
        private System.Windows.Forms.DataGridViewTextBoxColumn colStepNo;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStepName;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colEnable;
        private System.Windows.Forms.DataGridViewComboBoxColumn colRelayChannel;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colRelayOn;
        private System.Windows.Forms.DataGridViewComboBoxColumn colMeasureMode;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMin;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMax;
        private System.Windows.Forms.DataGridViewComboBoxColumn colUnit;
        private System.Windows.Forms.DataGridViewTextBoxColumn colWaitMs;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRetry;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colStopOnFail;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRemark;

        private System.Windows.Forms.Timer uiTimer;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tabMain = new System.Windows.Forms.TabControl();
            this.tabRun = new System.Windows.Forms.TabPage();
            this.tabSteps = new System.Windows.Forms.TabPage();
            this.gbDevice = new System.Windows.Forms.GroupBox();
            this.lblGdm = new System.Windows.Forms.Label();
            this.lblRelay = new System.Windows.Forms.Label();
            this.btnConnect = new System.Windows.Forms.Button();
            this.btnDisconnect = new System.Windows.Forms.Button();
            this.gbConfig = new System.Windows.Forms.GroupBox();
            this.lblLoop = new System.Windows.Forms.Label();
            this.cbLoopCount = new System.Windows.Forms.ComboBox();
            this.lblNgActionCap = new System.Windows.Forms.Label();
            this.cbNgAction = new System.Windows.Forms.ComboBox();
            this.gbControl = new System.Windows.Forms.GroupBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.gbStat = new System.Windows.Forms.GroupBox();
            this.lblCurrentLoop = new System.Windows.Forms.Label();
            this.lblPass = new System.Windows.Forms.Label();
            this.lblFail = new System.Windows.Forms.Label();
            this.lblElapsed = new System.Windows.Forms.Label();
            this.lblNgAction = new System.Windows.Forms.Label();
            this.gbLog = new System.Windows.Forms.GroupBox();
            this.txtLog = new System.Windows.Forms.RichTextBox();
            this.lblStepHint = new System.Windows.Forms.Label();
            this.dgvSteps = new System.Windows.Forms.DataGridView();
            this.btnReloadCfg = new System.Windows.Forms.Button();
            this.btnSaveCfg = new System.Windows.Forms.Button();
            this.btnDefaultCfg = new System.Windows.Forms.Button();
            this.colStepNo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStepName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colEnable = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colRelayChannel = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colRelayOn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colMeasureMode = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colMin = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMax = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colUnit = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colWaitMs = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRetry = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStopOnFail = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colRemark = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.uiTimer = new System.Windows.Forms.Timer(this.components);
            this.tabMain.SuspendLayout();
            this.tabRun.SuspendLayout();
            this.tabSteps.SuspendLayout();
            this.gbDevice.SuspendLayout();
            this.gbConfig.SuspendLayout();
            this.gbControl.SuspendLayout();
            this.gbStat.SuspendLayout();
            this.gbLog.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSteps)).BeginInit();
            this.SuspendLayout();
            //
            // tabMain
            //
            this.tabMain.Controls.Add(this.tabRun);
            this.tabMain.Controls.Add(this.tabSteps);
            this.tabMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabMain.Location = new System.Drawing.Point(0, 0);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.tabMain.Size = new System.Drawing.Size(774, 661);
            this.tabMain.TabIndex = 0;
            //
            // tabRun
            //
            this.tabRun.Controls.Add(this.gbDevice);
            this.tabRun.Controls.Add(this.gbConfig);
            this.tabRun.Controls.Add(this.gbControl);
            this.tabRun.Controls.Add(this.gbStat);
            this.tabRun.Controls.Add(this.gbLog);
            this.tabRun.Location = new System.Drawing.Point(4, 26);
            this.tabRun.Name = "tabRun";
            this.tabRun.Padding = new System.Windows.Forms.Padding(3);
            this.tabRun.Size = new System.Drawing.Size(766, 631);
            this.tabRun.TabIndex = 0;
            this.tabRun.Text = "壓力測試";
            this.tabRun.UseVisualStyleBackColor = true;
            //
            // tabSteps
            //
            this.tabSteps.Controls.Add(this.lblStepHint);
            this.tabSteps.Controls.Add(this.dgvSteps);
            this.tabSteps.Controls.Add(this.btnReloadCfg);
            this.tabSteps.Controls.Add(this.btnSaveCfg);
            this.tabSteps.Controls.Add(this.btnDefaultCfg);
            this.tabSteps.Location = new System.Drawing.Point(4, 26);
            this.tabSteps.Name = "tabSteps";
            this.tabSteps.Padding = new System.Windows.Forms.Padding(3);
            this.tabSteps.Size = new System.Drawing.Size(766, 631);
            this.tabSteps.TabIndex = 1;
            this.tabSteps.Text = "Step 設定";
            this.tabSteps.UseVisualStyleBackColor = true;
            //
            // gbDevice
            //
            this.gbDevice.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
            this.gbDevice.Controls.Add(this.lblGdm);
            this.gbDevice.Controls.Add(this.lblRelay);
            this.gbDevice.Controls.Add(this.btnConnect);
            this.gbDevice.Controls.Add(this.btnDisconnect);
            this.gbDevice.Location = new System.Drawing.Point(8, 8);
            this.gbDevice.Name = "gbDevice";
            this.gbDevice.Size = new System.Drawing.Size(750, 92);
            this.gbDevice.TabIndex = 0;
            this.gbDevice.TabStop = false;
            this.gbDevice.Text = "設備狀態";
            //
            // lblGdm
            //
            this.lblGdm.AutoSize = true;
            this.lblGdm.Location = new System.Drawing.Point(16, 28);
            this.lblGdm.Name = "lblGdm";
            this.lblGdm.Size = new System.Drawing.Size(110, 15);
            this.lblGdm.TabIndex = 0;
            this.lblGdm.Text = "GDM：Disconnected";
            //
            // lblRelay
            //
            this.lblRelay.AutoSize = true;
            this.lblRelay.Location = new System.Drawing.Point(16, 56);
            this.lblRelay.Name = "lblRelay";
            this.lblRelay.Size = new System.Drawing.Size(110, 15);
            this.lblRelay.TabIndex = 1;
            this.lblRelay.Text = "Relay：Disconnected";
            //
            // btnConnect
            //
            this.btnConnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnConnect.Location = new System.Drawing.Point(566, 24);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(84, 30);
            this.btnConnect.TabIndex = 2;
            this.btnConnect.Text = "連線設備";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.BtnConnect_Click);
            //
            // btnDisconnect
            //
            this.btnDisconnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDisconnect.Location = new System.Drawing.Point(656, 24);
            this.btnDisconnect.Name = "btnDisconnect";
            this.btnDisconnect.Size = new System.Drawing.Size(84, 30);
            this.btnDisconnect.TabIndex = 3;
            this.btnDisconnect.Text = "中斷";
            this.btnDisconnect.UseVisualStyleBackColor = true;
            this.btnDisconnect.Click += new System.EventHandler(this.BtnDisconnect_Click);
            //
            // gbConfig
            //
            this.gbConfig.Location = new System.Drawing.Point(8, 108);
            this.gbConfig.Name = "gbConfig";
            this.gbConfig.Size = new System.Drawing.Size(366, 120);
            this.gbConfig.Controls.Add(this.lblLoop);
            this.gbConfig.Controls.Add(this.cbLoopCount);
            this.gbConfig.Controls.Add(this.lblNgActionCap);
            this.gbConfig.Controls.Add(this.cbNgAction);
            this.gbConfig.TabIndex = 1;
            this.gbConfig.TabStop = false;
            this.gbConfig.Text = "壓力測試設定";
            //
            // lblLoop
            //
            this.lblLoop.AutoSize = true;
            this.lblLoop.Location = new System.Drawing.Point(16, 36);
            this.lblLoop.Name = "lblLoop";
            this.lblLoop.Size = new System.Drawing.Size(80, 15);
            this.lblLoop.TabIndex = 0;
            this.lblLoop.Text = "Loop Count：";
            //
            // cbLoopCount
            //
            this.cbLoopCount.FormattingEnabled = true;
            this.cbLoopCount.Items.AddRange(new object[] { "100", "500", "1000" });
            this.cbLoopCount.Location = new System.Drawing.Point(110, 32);
            this.cbLoopCount.Name = "cbLoopCount";
            this.cbLoopCount.Size = new System.Drawing.Size(120, 23);
            this.cbLoopCount.TabIndex = 1;
            //
            // lblNgActionCap
            //
            this.lblNgActionCap.AutoSize = true;
            this.lblNgActionCap.Location = new System.Drawing.Point(16, 78);
            this.lblNgActionCap.Name = "lblNgActionCap";
            this.lblNgActionCap.Size = new System.Drawing.Size(72, 15);
            this.lblNgActionCap.TabIndex = 2;
            this.lblNgActionCap.Text = "NG Action：";
            //
            // cbNgAction
            //
            this.cbNgAction.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbNgAction.FormattingEnabled = true;
            this.cbNgAction.Items.AddRange(new object[] { "Stop Test", "Continue Loop", "Continue Next Step" });
            this.cbNgAction.Location = new System.Drawing.Point(110, 74);
            this.cbNgAction.Name = "cbNgAction";
            this.cbNgAction.Size = new System.Drawing.Size(180, 23);
            this.cbNgAction.TabIndex = 3;
            this.cbNgAction.SelectedIndexChanged += new System.EventHandler(this.CbNgAction_Changed);
            //
            // gbControl
            //
            this.gbControl.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.gbControl.Location = new System.Drawing.Point(398, 108);
            this.gbControl.Name = "gbControl";
            this.gbControl.Size = new System.Drawing.Size(360, 120);
            this.gbControl.Controls.Add(this.btnStart);
            this.gbControl.Controls.Add(this.btnStop);
            this.gbControl.TabIndex = 2;
            this.gbControl.TabStop = false;
            this.gbControl.Text = "測試控制";
            //
            // btnStart
            //
            this.btnStart.Location = new System.Drawing.Point(24, 46);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(150, 40);
            this.btnStart.TabIndex = 0;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.BtnStart_Click);
            //
            // btnStop
            //
            this.btnStop.Location = new System.Drawing.Point(186, 46);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(150, 40);
            this.btnStop.TabIndex = 1;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.BtnStop_Click);
            //
            // gbStat
            //
            this.gbStat.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
            this.gbStat.Controls.Add(this.lblCurrentLoop);
            this.gbStat.Controls.Add(this.lblPass);
            this.gbStat.Controls.Add(this.lblFail);
            this.gbStat.Controls.Add(this.lblElapsed);
            this.gbStat.Controls.Add(this.lblNgAction);
            this.gbStat.Location = new System.Drawing.Point(8, 236);
            this.gbStat.Name = "gbStat";
            this.gbStat.Size = new System.Drawing.Size(750, 60);
            this.gbStat.TabIndex = 3;
            this.gbStat.TabStop = false;
            this.gbStat.Text = "統計資訊";
            //
            // lblCurrentLoop
            //
            this.lblCurrentLoop.AutoSize = true;
            this.lblCurrentLoop.Location = new System.Drawing.Point(16, 26);
            this.lblCurrentLoop.Name = "lblCurrentLoop";
            this.lblCurrentLoop.Size = new System.Drawing.Size(120, 15);
            this.lblCurrentLoop.TabIndex = 0;
            this.lblCurrentLoop.Text = "Current Loop：0 / 0";
            //
            // lblPass
            //
            this.lblPass.AutoSize = true;
            this.lblPass.ForeColor = System.Drawing.Color.FromArgb(46, 160, 67);
            this.lblPass.Location = new System.Drawing.Point(190, 26);
            this.lblPass.Name = "lblPass";
            this.lblPass.Size = new System.Drawing.Size(60, 15);
            this.lblPass.TabIndex = 1;
            this.lblPass.Text = "PASS：0";
            //
            // lblFail
            //
            this.lblFail.AutoSize = true;
            this.lblFail.ForeColor = System.Drawing.Color.FromArgb(211, 47, 47);
            this.lblFail.Location = new System.Drawing.Point(290, 26);
            this.lblFail.Name = "lblFail";
            this.lblFail.Size = new System.Drawing.Size(60, 15);
            this.lblFail.TabIndex = 2;
            this.lblFail.Text = "FAIL：0";
            //
            // lblElapsed
            //
            this.lblElapsed.AutoSize = true;
            this.lblElapsed.Location = new System.Drawing.Point(390, 26);
            this.lblElapsed.Name = "lblElapsed";
            this.lblElapsed.Size = new System.Drawing.Size(120, 15);
            this.lblElapsed.TabIndex = 3;
            this.lblElapsed.Text = "Elapsed：00:00:00";
            //
            // lblNgAction
            //
            this.lblNgAction.AutoSize = true;
            this.lblNgAction.ForeColor = System.Drawing.Color.RoyalBlue;
            this.lblNgAction.Location = new System.Drawing.Point(540, 26);
            this.lblNgAction.Name = "lblNgAction";
            this.lblNgAction.Size = new System.Drawing.Size(150, 15);
            this.lblNgAction.TabIndex = 4;
            this.lblNgAction.Text = "NG Action：Stop Test";
            //
            // gbLog
            //
            this.gbLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
            this.gbLog.Controls.Add(this.txtLog);
            this.gbLog.Location = new System.Drawing.Point(8, 304);
            this.gbLog.Name = "gbLog";
            this.gbLog.Size = new System.Drawing.Size(750, 319);
            this.gbLog.TabIndex = 4;
            this.gbLog.TabStop = false;
            this.gbLog.Text = "Log";
            //
            // txtLog
            //
            this.txtLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLog.BackColor = System.Drawing.Color.White;
            this.txtLog.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtLog.Location = new System.Drawing.Point(10, 22);
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.Size = new System.Drawing.Size(732, 289);
            this.txtLog.TabIndex = 0;
            this.txtLog.Text = "";
            this.txtLog.WordWrap = false;
            //
            // lblStepHint
            //
            this.lblStepHint.AutoSize = true;
            this.lblStepHint.Location = new System.Drawing.Point(8, 8);
            this.lblStepHint.Name = "lblStepHint";
            this.lblStepHint.Size = new System.Drawing.Size(400, 15);
            this.lblStepHint.TabIndex = 0;
            this.lblStepHint.Text = "Step1~Step12 壓力測試參數（編輯後請按「儲存」寫入 Config\\StressTestConfig.json）";
            //
            // dgvSteps
            //
            this.dgvSteps.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvSteps.AllowUserToAddRows = false;
            this.dgvSteps.AllowUserToDeleteRows = false;
            this.dgvSteps.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSteps.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colStepNo, this.colStepName, this.colEnable, this.colRelayChannel, this.colRelayOn,
                this.colMeasureMode, this.colMin, this.colMax, this.colUnit, this.colWaitMs,
                this.colRetry, this.colStopOnFail, this.colRemark});
            this.dgvSteps.Location = new System.Drawing.Point(8, 32);
            this.dgvSteps.Name = "dgvSteps";
            this.dgvSteps.RowHeadersWidth = 24;
            this.dgvSteps.Size = new System.Drawing.Size(750, 540);
            this.dgvSteps.TabIndex = 1;
            this.dgvSteps.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.DgvSteps_DataError);
            //
            // colStepNo
            //
            this.colStepNo.HeaderText = "Step";
            this.colStepNo.Name = "colStepNo";
            this.colStepNo.ReadOnly = true;
            this.colStepNo.Width = 46;
            //
            // colStepName
            //
            this.colStepName.HeaderText = "Step Name";
            this.colStepName.Name = "colStepName";
            this.colStepName.Width = 130;
            //
            // colEnable
            //
            this.colEnable.HeaderText = "Enable";
            this.colEnable.Name = "colEnable";
            this.colEnable.Width = 52;
            //
            // colRelayChannel
            //
            this.colRelayChannel.HeaderText = "Relay Ch";
            this.colRelayChannel.Name = "colRelayChannel";
            this.colRelayChannel.Items.AddRange(new object[] { "None", "1", "2", "Both" });
            this.colRelayChannel.Width = 72;
            //
            // colRelayOn
            //
            this.colRelayOn.HeaderText = "Relay ON";
            this.colRelayOn.Name = "colRelayOn";
            this.colRelayOn.Width = 62;
            //
            // colMeasureMode
            //
            this.colMeasureMode.HeaderText = "Mode";
            this.colMeasureMode.Name = "colMeasureMode";
            this.colMeasureMode.Items.AddRange(new object[] { "Voltage", "Current", "Resistance" });
            this.colMeasureMode.Width = 92;
            //
            // colMin
            //
            this.colMin.HeaderText = "Min";
            this.colMin.Name = "colMin";
            this.colMin.Width = 72;
            //
            // colMax
            //
            this.colMax.HeaderText = "Max";
            this.colMax.Name = "colMax";
            this.colMax.Width = 72;
            //
            // colUnit
            //
            this.colUnit.HeaderText = "Unit";
            this.colUnit.Name = "colUnit";
            this.colUnit.Items.AddRange(new object[] { "V", "mA", "A", "Ω" });
            this.colUnit.Width = 52;
            //
            // colWaitMs
            //
            this.colWaitMs.HeaderText = "WaitMs";
            this.colWaitMs.Name = "colWaitMs";
            this.colWaitMs.Width = 64;
            //
            // colRetry
            //
            this.colRetry.HeaderText = "Retry";
            this.colRetry.Name = "colRetry";
            this.colRetry.Width = 50;
            //
            // colStopOnFail
            //
            this.colStopOnFail.HeaderText = "StopOnFail";
            this.colStopOnFail.Name = "colStopOnFail";
            this.colStopOnFail.Width = 76;
            //
            // colRemark
            //
            this.colRemark.HeaderText = "備註";
            this.colRemark.Name = "colRemark";
            this.colRemark.Width = 150;
            //
            // btnReloadCfg
            //
            this.btnReloadCfg.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnReloadCfg.Location = new System.Drawing.Point(8, 582);
            this.btnReloadCfg.Name = "btnReloadCfg";
            this.btnReloadCfg.Size = new System.Drawing.Size(110, 34);
            this.btnReloadCfg.TabIndex = 2;
            this.btnReloadCfg.Text = "重新載入";
            this.btnReloadCfg.UseVisualStyleBackColor = true;
            this.btnReloadCfg.Click += new System.EventHandler(this.BtnReloadCfg_Click);
            //
            // btnSaveCfg
            //
            this.btnSaveCfg.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnSaveCfg.Location = new System.Drawing.Point(126, 582);
            this.btnSaveCfg.Name = "btnSaveCfg";
            this.btnSaveCfg.Size = new System.Drawing.Size(110, 34);
            this.btnSaveCfg.TabIndex = 3;
            this.btnSaveCfg.Text = "儲存";
            this.btnSaveCfg.UseVisualStyleBackColor = true;
            this.btnSaveCfg.Click += new System.EventHandler(this.BtnSaveCfg_Click);
            //
            // btnDefaultCfg
            //
            this.btnDefaultCfg.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDefaultCfg.Location = new System.Drawing.Point(244, 582);
            this.btnDefaultCfg.Name = "btnDefaultCfg";
            this.btnDefaultCfg.Size = new System.Drawing.Size(110, 34);
            this.btnDefaultCfg.TabIndex = 4;
            this.btnDefaultCfg.Text = "還原預設";
            this.btnDefaultCfg.UseVisualStyleBackColor = true;
            this.btnDefaultCfg.Click += new System.EventHandler(this.BtnDefaultCfg_Click);
            //
            // uiTimer
            //
            this.uiTimer.Interval = 500;
            this.uiTimer.Tick += new System.EventHandler(this.UiTimer_Tick);
            //
            // StressForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(774, 661);
            this.Controls.Add(this.tabMain);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.MinimumSize = new System.Drawing.Size(700, 560);
            this.Name = "StressForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "DX01 Stress Tester";
            this.Load += new System.EventHandler(this.StressForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.StressForm_FormClosing);
            this.tabMain.ResumeLayout(false);
            this.tabRun.ResumeLayout(false);
            this.tabSteps.ResumeLayout(false);
            this.tabSteps.PerformLayout();
            this.gbDevice.ResumeLayout(false);
            this.gbDevice.PerformLayout();
            this.gbConfig.ResumeLayout(false);
            this.gbConfig.PerformLayout();
            this.gbControl.ResumeLayout(false);
            this.gbStat.ResumeLayout(false);
            this.gbStat.PerformLayout();
            this.gbLog.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvSteps)).EndInit();
            this.ResumeLayout(false);
        }
    }
}
