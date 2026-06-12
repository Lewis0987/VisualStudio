namespace DX01_ShortCircuitTester
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.tabMain = new System.Windows.Forms.TabControl();
            this.tabTest = new System.Windows.Forms.TabPage();
            this.tabDevice = new System.Windows.Forms.TabPage();
            this.tableRoot = new System.Windows.Forms.TableLayoutPanel();
            this.panelTop = new System.Windows.Forms.TableLayoutPanel();
            this.lblBarcodeCaption = new System.Windows.Forms.Label();
            this.txtBarcode = new System.Windows.Forms.TextBox();
            this.lblBarcodeMsg = new System.Windows.Forms.Label();
            this.panelStatus = new System.Windows.Forms.TableLayoutPanel();
            this.panelStatusLeft = new System.Windows.Forms.TableLayoutPanel();
            this.capStep = new System.Windows.Forms.Label();
            this.lblCurrentStep = new System.Windows.Forms.Label();
            this.capRelay = new System.Windows.Forms.Label();
            this.lblRelay = new System.Windows.Forms.Label();
            this.capMeasure = new System.Windows.Forms.Label();
            this.lblMeasure = new System.Windows.Forms.Label();
            this.lblResult = new System.Windows.Forms.Label();
            this.dgvResults = new System.Windows.Forms.DataGridView();
            this.colStep = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRelay = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMode = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLimit = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colResult = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panelFooter = new System.Windows.Forms.Panel();
            this.lblConnGdm = new System.Windows.Forms.Label();
            this.lblConnRelay = new System.Windows.Forms.Label();
            this.lblInfo = new System.Windows.Forms.Label();
            this.gbGdm = new System.Windows.Forms.GroupBox();
            this.lblGdmPortCap = new System.Windows.Forms.Label();
            this.cbGdmPort = new System.Windows.Forms.ComboBox();
            this.btnGdmRefresh = new System.Windows.Forms.Button();
            this.lblGdmBaudCap = new System.Windows.Forms.Label();
            this.cbGdmBaud = new System.Windows.Forms.ComboBox();
            this.btnGdmConnect = new System.Windows.Forms.Button();
            this.btnGdmDisconnect = new System.Windows.Forms.Button();
            this.lblGdmStatus = new System.Windows.Forms.Label();
            this.lblGdmIdn = new System.Windows.Forms.Label();
            this.rbSerial = new System.Windows.Forms.RadioButton();
            this.rbLan = new System.Windows.Forms.RadioButton();
            this.lblGdmIpCap = new System.Windows.Forms.Label();
            this.txtGdmIp = new System.Windows.Forms.TextBox();
            this.lblGdmTcpPortCap = new System.Windows.Forms.Label();
            this.txtGdmPort = new System.Windows.Forms.TextBox();
            this.gbRelay = new System.Windows.Forms.GroupBox();
            this.lblRelayInfo = new System.Windows.Forms.Label();
            this.lblRelayStatus = new System.Windows.Forms.Label();
            this.btnRelayConnect = new System.Windows.Forms.Button();
            this.btnRelayDisconnect = new System.Windows.Forms.Button();
            this.tabMain.SuspendLayout();
            this.tabTest.SuspendLayout();
            this.tabDevice.SuspendLayout();
            this.tableRoot.SuspendLayout();
            this.panelTop.SuspendLayout();
            this.panelStatus.SuspendLayout();
            this.panelStatusLeft.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).BeginInit();
            this.panelFooter.SuspendLayout();
            this.gbGdm.SuspendLayout();
            this.gbRelay.SuspendLayout();
            this.SuspendLayout();
            //
            // tabMain
            //
            this.tabMain.Controls.Add(this.tabTest);
            this.tabMain.Controls.Add(this.tabDevice);
            this.tabMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabMain.Font = new System.Drawing.Font("Microsoft JhengHei UI", 11F);
            this.tabMain.Location = new System.Drawing.Point(0, 0);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.tabMain.Size = new System.Drawing.Size(984, 720);
            this.tabMain.TabIndex = 0;
            //
            // tabTest
            //
            this.tabTest.Controls.Add(this.tableRoot);
            this.tabTest.Location = new System.Drawing.Point(4, 28);
            this.tabTest.Name = "tabTest";
            this.tabTest.Size = new System.Drawing.Size(976, 688);
            this.tabTest.TabIndex = 0;
            this.tabTest.Text = "Test";
            this.tabTest.UseVisualStyleBackColor = true;
            //
            // tabDevice
            //
            this.tabDevice.Controls.Add(this.gbRelay);
            this.tabDevice.Controls.Add(this.gbGdm);
            this.tabDevice.Location = new System.Drawing.Point(4, 28);
            this.tabDevice.Name = "tabDevice";
            this.tabDevice.Padding = new System.Windows.Forms.Padding(12);
            this.tabDevice.Size = new System.Drawing.Size(976, 688);
            this.tabDevice.TabIndex = 1;
            this.tabDevice.Text = "Settings";
            this.tabDevice.UseVisualStyleBackColor = true;
            //
            // tableRoot
            //
            this.tableRoot.ColumnCount = 1;
            this.tableRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableRoot.Controls.Add(this.panelTop, 0, 0);
            this.tableRoot.Controls.Add(this.panelStatus, 0, 1);
            this.tableRoot.Controls.Add(this.dgvResults, 0, 2);
            this.tableRoot.Controls.Add(this.panelFooter, 0, 3);
            this.tableRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableRoot.Location = new System.Drawing.Point(0, 0);
            this.tableRoot.Name = "tableRoot";
            this.tableRoot.Padding = new System.Windows.Forms.Padding(8);
            this.tableRoot.RowCount = 4;
            this.tableRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 78F));
            this.tableRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableRoot.Name = "tableRoot";
            this.tableRoot.TabIndex = 0;
            //
            // panelTop
            //
            this.panelTop.ColumnCount = 3;
            this.panelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.panelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 260F));
            this.panelTop.Controls.Add(this.lblBarcodeCaption, 0, 0);
            this.panelTop.Controls.Add(this.txtBarcode, 1, 0);
            this.panelTop.Controls.Add(this.lblBarcodeMsg, 2, 0);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelTop.Location = new System.Drawing.Point(11, 11);
            this.panelTop.Name = "panelTop";
            this.panelTop.RowCount = 1;
            this.panelTop.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelTop.Size = new System.Drawing.Size(954, 72);
            this.panelTop.TabIndex = 0;
            //
            // lblBarcodeCaption
            //
            this.lblBarcodeCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblBarcodeCaption.AutoSize = true;
            this.lblBarcodeCaption.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F);
            this.lblBarcodeCaption.Location = new System.Drawing.Point(3, 25);
            this.lblBarcodeCaption.Name = "lblBarcodeCaption";
            this.lblBarcodeCaption.Size = new System.Drawing.Size(94, 21);
            this.lblBarcodeCaption.TabIndex = 0;
            this.lblBarcodeCaption.Text = "條碼/序號:";
            //
            // txtBarcode
            //
            this.txtBarcode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.txtBarcode.Font = new System.Drawing.Font("Consolas", 18F);
            this.txtBarcode.Location = new System.Drawing.Point(106, 18);
            this.txtBarcode.Name = "txtBarcode";
            this.txtBarcode.Size = new System.Drawing.Size(491, 36);
            this.txtBarcode.TabIndex = 1;
            //
            // lblBarcodeMsg
            //
            this.lblBarcodeMsg.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblBarcodeMsg.AutoSize = true;
            this.lblBarcodeMsg.Font = new System.Drawing.Font("Microsoft JhengHei UI", 13F, System.Drawing.FontStyle.Bold);
            this.lblBarcodeMsg.Location = new System.Drawing.Point(700, 24);
            this.lblBarcodeMsg.Name = "lblBarcodeMsg";
            this.lblBarcodeMsg.Size = new System.Drawing.Size(0, 24);
            this.lblBarcodeMsg.TabIndex = 2;
            //
            // panelStatus
            //
            this.panelStatus.ColumnCount = 2;
            this.panelStatus.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 58F));
            this.panelStatus.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 42F));
            this.panelStatus.Controls.Add(this.panelStatusLeft, 0, 0);
            this.panelStatus.Controls.Add(this.lblResult, 1, 0);
            this.panelStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelStatus.Location = new System.Drawing.Point(11, 89);
            this.panelStatus.Name = "panelStatus";
            this.panelStatus.RowCount = 1;
            this.panelStatus.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelStatus.Size = new System.Drawing.Size(954, 174);
            this.panelStatus.TabIndex = 1;
            //
            // panelStatusLeft
            //
            this.panelStatusLeft.ColumnCount = 2;
            this.panelStatusLeft.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.panelStatusLeft.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelStatusLeft.Controls.Add(this.capStep, 0, 0);
            this.panelStatusLeft.Controls.Add(this.lblCurrentStep, 1, 0);
            this.panelStatusLeft.Controls.Add(this.capRelay, 0, 1);
            this.panelStatusLeft.Controls.Add(this.lblRelay, 1, 1);
            this.panelStatusLeft.Controls.Add(this.capMeasure, 0, 2);
            this.panelStatusLeft.Controls.Add(this.lblMeasure, 1, 2);
            this.panelStatusLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelStatusLeft.Location = new System.Drawing.Point(3, 3);
            this.panelStatusLeft.Name = "panelStatusLeft";
            this.panelStatusLeft.RowCount = 3;
            this.panelStatusLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 34F));
            this.panelStatusLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33F));
            this.panelStatusLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33F));
            this.panelStatusLeft.Size = new System.Drawing.Size(547, 168);
            this.panelStatusLeft.TabIndex = 0;
            //
            // capStep
            //
            this.capStep.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.capStep.AutoSize = true;
            this.capStep.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F);
            this.capStep.Location = new System.Drawing.Point(3, 20);
            this.capStep.Name = "capStep";
            this.capStep.Size = new System.Drawing.Size(78, 21);
            this.capStep.TabIndex = 0;
            this.capStep.Text = "目前步驟:";
            //
            // lblCurrentStep
            //
            this.lblCurrentStep.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblCurrentStep.AutoEllipsis = true;
            this.lblCurrentStep.AutoSize = false;
            this.lblCurrentStep.Font = new System.Drawing.Font("Microsoft JhengHei UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblCurrentStep.Location = new System.Drawing.Point(123, 3);
            this.lblCurrentStep.Name = "lblCurrentStep";
            this.lblCurrentStep.Size = new System.Drawing.Size(421, 50);
            this.lblCurrentStep.TabIndex = 1;
            this.lblCurrentStep.Text = "待測";
            this.lblCurrentStep.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // capRelay
            //
            this.capRelay.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.capRelay.AutoSize = true;
            this.capRelay.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F);
            this.capRelay.Location = new System.Drawing.Point(3, 76);
            this.capRelay.Name = "capRelay";
            this.capRelay.Size = new System.Drawing.Size(86, 21);
            this.capRelay.TabIndex = 2;
            this.capRelay.Text = "Relay 狀態:";
            //
            // lblRelay
            //
            this.lblRelay.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblRelay.AutoSize = true;
            this.lblRelay.Font = new System.Drawing.Font("Consolas", 16F, System.Drawing.FontStyle.Bold);
            this.lblRelay.Location = new System.Drawing.Point(123, 72);
            this.lblRelay.Name = "lblRelay";
            this.lblRelay.Size = new System.Drawing.Size(56, 26);
            this.lblRelay.TabIndex = 3;
            this.lblRelay.Text = "--";
            //
            // capMeasure
            //
            this.capMeasure.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.capMeasure.AutoSize = true;
            this.capMeasure.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F);
            this.capMeasure.Location = new System.Drawing.Point(3, 131);
            this.capMeasure.Name = "capMeasure";
            this.capMeasure.Size = new System.Drawing.Size(69, 21);
            this.capMeasure.TabIndex = 4;
            this.capMeasure.Text = "量測值:";
            //
            // lblMeasure
            //
            this.lblMeasure.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblMeasure.AutoSize = true;
            this.lblMeasure.Font = new System.Drawing.Font("Consolas", 20F, System.Drawing.FontStyle.Bold);
            this.lblMeasure.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(33)))), ((int)(((byte)(33)))), ((int)(((byte)(33)))));
            this.lblMeasure.Location = new System.Drawing.Point(123, 124);
            this.lblMeasure.Name = "lblMeasure";
            this.lblMeasure.Size = new System.Drawing.Size(54, 32);
            this.lblMeasure.TabIndex = 5;
            this.lblMeasure.Text = "---";
            //
            // lblResult
            //
            this.lblResult.BackColor = System.Drawing.Color.Gainsboro;
            this.lblResult.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblResult.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblResult.Font = new System.Drawing.Font("Microsoft JhengHei UI", 60F, System.Drawing.FontStyle.Bold);
            this.lblResult.ForeColor = System.Drawing.Color.DimGray;
            this.lblResult.Location = new System.Drawing.Point(556, 3);
            this.lblResult.Name = "lblResult";
            this.lblResult.Size = new System.Drawing.Size(395, 168);
            this.lblResult.TabIndex = 1;
            this.lblResult.Text = "待測";
            this.lblResult.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // dgvResults
            //
            this.dgvResults.AllowUserToAddRows = false;
            this.dgvResults.AllowUserToDeleteRows = false;
            this.dgvResults.AllowUserToResizeColumns = true;
            this.dgvResults.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvResults.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.dgvResults.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            this.dgvResults.ColumnHeadersHeight = 32;
            this.dgvResults.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colStep,
                this.colName,
                this.colRelay,
                this.colMode,
                this.colValue,
                this.colLimit,
                this.colResult});
            this.dgvResults.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvResults.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.dgvResults.Font = new System.Drawing.Font("Microsoft JhengHei UI", 11F);
            this.dgvResults.Location = new System.Drawing.Point(11, 269);
            this.dgvResults.Name = "dgvResults";
            this.dgvResults.ReadOnly = true;
            this.dgvResults.RowHeadersVisible = false;
            this.dgvResults.RowTemplate.Height = 28;
            this.dgvResults.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvResults.Size = new System.Drawing.Size(954, 402);
            this.dgvResults.TabIndex = 2;
            //
            // colStep
            //
            this.colStep.FillWeight = 60F;
            this.colStep.HeaderText = "Step";
            this.colStep.Name = "colStep";
            this.colStep.ReadOnly = true;
            //
            // colName
            //
            this.colName.FillWeight = 180F;
            this.colName.HeaderText = "名稱";
            this.colName.Name = "colName";
            this.colName.ReadOnly = true;
            //
            // colRelay
            //
            this.colRelay.FillWeight = 80F;
            this.colRelay.HeaderText = "Relay";
            this.colRelay.Name = "colRelay";
            this.colRelay.ReadOnly = true;
            //
            // colMode
            //
            this.colMode.FillWeight = 120F;
            this.colMode.HeaderText = "模式";
            this.colMode.Name = "colMode";
            this.colMode.ReadOnly = true;
            //
            // colValue
            //
            this.colValue.FillWeight = 140F;
            this.colValue.HeaderText = "量測值";
            this.colValue.Name = "colValue";
            this.colValue.ReadOnly = true;
            //
            // colLimit
            //
            this.colLimit.FillWeight = 180F;
            this.colLimit.HeaderText = "判定條件";
            this.colLimit.Name = "colLimit";
            this.colLimit.ReadOnly = true;
            //
            // colResult
            //
            this.colResult.FillWeight = 100F;
            this.colResult.HeaderText = "結果";
            this.colResult.Name = "colResult";
            this.colResult.ReadOnly = true;
            //
            // panelFooter
            //
            this.panelFooter.Controls.Add(this.lblInfo);
            this.panelFooter.Controls.Add(this.lblConnGdm);
            this.panelFooter.Controls.Add(this.lblConnRelay);
            this.panelFooter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelFooter.Location = new System.Drawing.Point(11, 685);
            this.panelFooter.Name = "panelFooter";
            this.panelFooter.Size = new System.Drawing.Size(954, 24);
            this.panelFooter.TabIndex = 3;
            //
            // lblConnGdm
            //
            this.lblConnGdm.AutoSize = true;
            this.lblConnGdm.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblConnGdm.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9.5F);
            this.lblConnGdm.ForeColor = System.Drawing.Color.Red;
            this.lblConnGdm.Name = "lblConnGdm";
            this.lblConnGdm.Padding = new System.Windows.Forms.Padding(0, 0, 16, 0);
            this.lblConnGdm.TabIndex = 1;
            this.lblConnGdm.Text = "電表: 未連線";
            this.lblConnGdm.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // lblConnRelay
            //
            this.lblConnRelay.AutoSize = true;
            this.lblConnRelay.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblConnRelay.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9.5F);
            this.lblConnRelay.ForeColor = System.Drawing.Color.Red;
            this.lblConnRelay.Name = "lblConnRelay";
            this.lblConnRelay.TabIndex = 2;
            this.lblConnRelay.Text = "Relay: 未連線";
            this.lblConnRelay.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // lblInfo
            //
            this.lblInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblInfo.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9.5F);
            this.lblInfo.ForeColor = System.Drawing.Color.DimGray;
            this.lblInfo.Location = new System.Drawing.Point(0, 0);
            this.lblInfo.Name = "lblInfo";
            this.lblInfo.Size = new System.Drawing.Size(594, 24);
            this.lblInfo.TabIndex = 0;
            this.lblInfo.Text = "就緒";
            this.lblInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // gbGdm
            //
            this.gbGdm.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.gbGdm.Controls.Add(this.rbSerial);
            this.gbGdm.Controls.Add(this.rbLan);
            this.gbGdm.Controls.Add(this.lblGdmPortCap);
            this.gbGdm.Controls.Add(this.cbGdmPort);
            this.gbGdm.Controls.Add(this.btnGdmRefresh);
            this.gbGdm.Controls.Add(this.lblGdmBaudCap);
            this.gbGdm.Controls.Add(this.cbGdmBaud);
            this.gbGdm.Controls.Add(this.lblGdmIpCap);
            this.gbGdm.Controls.Add(this.txtGdmIp);
            this.gbGdm.Controls.Add(this.lblGdmTcpPortCap);
            this.gbGdm.Controls.Add(this.txtGdmPort);
            this.gbGdm.Controls.Add(this.btnGdmConnect);
            this.gbGdm.Controls.Add(this.btnGdmDisconnect);
            this.gbGdm.Controls.Add(this.lblGdmStatus);
            this.gbGdm.Controls.Add(this.lblGdmIdn);
            this.gbGdm.Font = new System.Drawing.Font("Microsoft JhengHei UI", 11F);
            this.gbGdm.Location = new System.Drawing.Point(16, 16);
            this.gbGdm.Name = "gbGdm";
            this.gbGdm.Size = new System.Drawing.Size(944, 210);
            this.gbGdm.TabIndex = 0;
            this.gbGdm.TabStop = false;
            this.gbGdm.Text = "GDM-8261A 電表 (RS-232 / USB 序列 / LAN)";
            //
            // rbSerial
            //
            this.rbSerial.AutoSize = true;
            this.rbSerial.Location = new System.Drawing.Point(150, 26);
            this.rbSerial.Name = "rbSerial";
            this.rbSerial.Size = new System.Drawing.Size(63, 23);
            this.rbSerial.TabIndex = 1;
            this.rbSerial.Text = "Serial";
            this.rbSerial.UseVisualStyleBackColor = true;
            //
            // rbLan
            //
            this.rbLan.AutoSize = true;
            this.rbLan.Checked = true;
            this.rbLan.Location = new System.Drawing.Point(20, 26);
            this.rbLan.Name = "rbLan";
            this.rbLan.Size = new System.Drawing.Size(92, 23);
            this.rbLan.TabIndex = 0;
            this.rbLan.TabStop = true;
            this.rbLan.Text = "LAN (TCP)";
            this.rbLan.UseVisualStyleBackColor = true;
            //
            // lblGdmPortCap
            //
            this.lblGdmPortCap.AutoSize = true;
            this.lblGdmPortCap.Location = new System.Drawing.Point(20, 61);
            this.lblGdmPortCap.Name = "lblGdmPortCap";
            this.lblGdmPortCap.Size = new System.Drawing.Size(80, 19);
            this.lblGdmPortCap.TabIndex = 2;
            this.lblGdmPortCap.Text = "COM Port:";
            //
            // cbGdmPort
            //
            this.cbGdmPort.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbGdmPort.Location = new System.Drawing.Point(120, 58);
            this.cbGdmPort.Name = "cbGdmPort";
            this.cbGdmPort.Size = new System.Drawing.Size(150, 27);
            this.cbGdmPort.TabIndex = 3;
            //
            // btnGdmRefresh
            //
            this.btnGdmRefresh.Location = new System.Drawing.Point(284, 57);
            this.btnGdmRefresh.Name = "btnGdmRefresh";
            this.btnGdmRefresh.Size = new System.Drawing.Size(140, 30);
            this.btnGdmRefresh.TabIndex = 4;
            this.btnGdmRefresh.Text = "搜尋 COM Port";
            this.btnGdmRefresh.UseVisualStyleBackColor = true;
            this.btnGdmRefresh.Click += new System.EventHandler(this.btnGdmRefresh_Click);
            //
            // lblGdmBaudCap
            //
            this.lblGdmBaudCap.AutoSize = true;
            this.lblGdmBaudCap.Location = new System.Drawing.Point(20, 99);
            this.lblGdmBaudCap.Name = "lblGdmBaudCap";
            this.lblGdmBaudCap.Size = new System.Drawing.Size(84, 19);
            this.lblGdmBaudCap.TabIndex = 5;
            this.lblGdmBaudCap.Text = "Baud Rate:";
            //
            // cbGdmBaud
            //
            this.cbGdmBaud.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbGdmBaud.Location = new System.Drawing.Point(120, 96);
            this.cbGdmBaud.Name = "cbGdmBaud";
            this.cbGdmBaud.Size = new System.Drawing.Size(150, 27);
            this.cbGdmBaud.TabIndex = 6;
            //
            // lblGdmIpCap
            //
            this.lblGdmIpCap.AutoSize = true;
            this.lblGdmIpCap.Location = new System.Drawing.Point(20, 61);
            this.lblGdmIpCap.Name = "lblGdmIpCap";
            this.lblGdmIpCap.Size = new System.Drawing.Size(86, 19);
            this.lblGdmIpCap.TabIndex = 9;
            this.lblGdmIpCap.Text = "IP Address:";
            this.lblGdmIpCap.Visible = false;
            //
            // txtGdmIp
            //
            this.txtGdmIp.Location = new System.Drawing.Point(120, 58);
            this.txtGdmIp.Name = "txtGdmIp";
            this.txtGdmIp.Size = new System.Drawing.Size(160, 27);
            this.txtGdmIp.TabIndex = 10;
            this.txtGdmIp.Text = "192.168.100.100";
            this.txtGdmIp.Visible = false;
            //
            // lblGdmTcpPortCap
            //
            this.lblGdmTcpPortCap.AutoSize = true;
            this.lblGdmTcpPortCap.Location = new System.Drawing.Point(20, 99);
            this.lblGdmTcpPortCap.Name = "lblGdmTcpPortCap";
            this.lblGdmTcpPortCap.Size = new System.Drawing.Size(40, 19);
            this.lblGdmTcpPortCap.TabIndex = 11;
            this.lblGdmTcpPortCap.Text = "Port:";
            this.lblGdmTcpPortCap.Visible = false;
            //
            // txtGdmPort
            //
            this.txtGdmPort.Location = new System.Drawing.Point(120, 96);
            this.txtGdmPort.Name = "txtGdmPort";
            this.txtGdmPort.Size = new System.Drawing.Size(90, 27);
            this.txtGdmPort.TabIndex = 12;
            this.txtGdmPort.Text = "23";
            this.txtGdmPort.Visible = false;
            //
            // btnGdmConnect
            //
            this.btnGdmConnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnGdmConnect.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.btnGdmConnect.FlatAppearance.BorderSize = 0;
            this.btnGdmConnect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnGdmConnect.ForeColor = System.Drawing.Color.White;
            this.btnGdmConnect.Location = new System.Drawing.Point(688, 50);
            this.btnGdmConnect.Name = "btnGdmConnect";
            this.btnGdmConnect.Size = new System.Drawing.Size(110, 44);
            this.btnGdmConnect.TabIndex = 7;
            this.btnGdmConnect.Text = "連線";
            this.btnGdmConnect.UseVisualStyleBackColor = false;
            this.btnGdmConnect.Click += new System.EventHandler(this.btnGdmConnect_Click);
            //
            // btnGdmDisconnect
            //
            this.btnGdmDisconnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnGdmDisconnect.BackColor = System.Drawing.SystemColors.Control;
            this.btnGdmDisconnect.FlatAppearance.BorderSize = 0;
            this.btnGdmDisconnect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnGdmDisconnect.Location = new System.Drawing.Point(810, 50);
            this.btnGdmDisconnect.Name = "btnGdmDisconnect";
            this.btnGdmDisconnect.Size = new System.Drawing.Size(110, 44);
            this.btnGdmDisconnect.TabIndex = 8;
            this.btnGdmDisconnect.Text = "中斷連線";
            this.btnGdmDisconnect.UseVisualStyleBackColor = false;
            this.btnGdmDisconnect.Click += new System.EventHandler(this.btnGdmDisconnect_Click);
            //
            // lblGdmStatus
            //
            this.lblGdmStatus.AutoEllipsis = true;
            this.lblGdmStatus.AutoSize = false;
            this.lblGdmStatus.Font = new System.Drawing.Font("Microsoft JhengHei UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblGdmStatus.ForeColor = System.Drawing.Color.Red;
            this.lblGdmStatus.Location = new System.Drawing.Point(20, 128);
            this.lblGdmStatus.Name = "lblGdmStatus";
            this.lblGdmStatus.Size = new System.Drawing.Size(910, 74);
            this.lblGdmStatus.TabIndex = 13;
            this.lblGdmStatus.Text = "● 未連線";
            //
            // lblGdmIdn
            //
            this.lblGdmIdn.AutoSize = true;
            this.lblGdmIdn.ForeColor = System.Drawing.Color.DimGray;
            this.lblGdmIdn.Location = new System.Drawing.Point(180, 136);
            this.lblGdmIdn.Name = "lblGdmIdn";
            this.lblGdmIdn.Size = new System.Drawing.Size(0, 19);
            this.lblGdmIdn.TabIndex = 14;
            this.lblGdmIdn.Visible = false;
            //
            // gbRelay
            //
            this.gbRelay.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.gbRelay.Controls.Add(this.lblRelayInfo);
            this.gbRelay.Controls.Add(this.lblRelayStatus);
            this.gbRelay.Controls.Add(this.btnRelayConnect);
            this.gbRelay.Controls.Add(this.btnRelayDisconnect);
            this.gbRelay.Font = new System.Drawing.Font("Microsoft JhengHei UI", 11F);
            this.gbRelay.Location = new System.Drawing.Point(16, 240);
            this.gbRelay.Name = "gbRelay";
            this.gbRelay.Size = new System.Drawing.Size(944, 150);
            this.gbRelay.TabIndex = 1;
            this.gbRelay.TabStop = false;
            this.gbRelay.Text = "Relay Board (USB HID 16C0:05DF)";
            //
            // lblRelayInfo
            //
            this.lblRelayInfo.AutoSize = true;
            this.lblRelayInfo.ForeColor = System.Drawing.Color.DimGray;
            this.lblRelayInfo.Location = new System.Drawing.Point(24, 40);
            this.lblRelayInfo.Name = "lblRelayInfo";
            this.lblRelayInfo.Size = new System.Drawing.Size(360, 19);
            this.lblRelayInfo.TabIndex = 0;
            this.lblRelayInfo.Text = "USB HID 自動偵測 (VID 16C0 / PID 05DF)，免設定 COM。";
            //
            // lblRelayStatus
            //
            this.lblRelayStatus.AutoSize = true;
            this.lblRelayStatus.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblRelayStatus.ForeColor = System.Drawing.Color.Firebrick;
            this.lblRelayStatus.Location = new System.Drawing.Point(24, 78);
            this.lblRelayStatus.Name = "lblRelayStatus";
            this.lblRelayStatus.Size = new System.Drawing.Size(86, 21);
            this.lblRelayStatus.TabIndex = 1;
            this.lblRelayStatus.Text = "● 未連線";
            //
            // btnRelayConnect
            //
            this.btnRelayConnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRelayConnect.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.btnRelayConnect.FlatAppearance.BorderSize = 0;
            this.btnRelayConnect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRelayConnect.ForeColor = System.Drawing.Color.White;
            this.btnRelayConnect.Location = new System.Drawing.Point(688, 70);
            this.btnRelayConnect.Name = "btnRelayConnect";
            this.btnRelayConnect.Size = new System.Drawing.Size(110, 44);
            this.btnRelayConnect.TabIndex = 3;
            this.btnRelayConnect.Text = "連線";
            this.btnRelayConnect.UseVisualStyleBackColor = false;
            this.btnRelayConnect.Click += new System.EventHandler(this.btnRelayConnect_Click);
            //
            // btnRelayDisconnect
            //
            this.btnRelayDisconnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRelayDisconnect.BackColor = System.Drawing.SystemColors.Control;
            this.btnRelayDisconnect.FlatAppearance.BorderSize = 0;
            this.btnRelayDisconnect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRelayDisconnect.Location = new System.Drawing.Point(810, 70);
            this.btnRelayDisconnect.Name = "btnRelayDisconnect";
            this.btnRelayDisconnect.Size = new System.Drawing.Size(110, 44);
            this.btnRelayDisconnect.TabIndex = 4;
            this.btnRelayDisconnect.Text = "中斷連線";
            this.btnRelayDisconnect.UseVisualStyleBackColor = false;
            this.btnRelayDisconnect.Click += new System.EventHandler(this.btnRelayDisconnect_Click);
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(984, 720);
            this.Controls.Add(this.tabMain);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9.5F);
            this.MinimumSize = new System.Drawing.Size(840, 620);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "DX01 外殼短路流程測試";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.tabMain.ResumeLayout(false);
            this.tabTest.ResumeLayout(false);
            this.tabDevice.ResumeLayout(false);
            this.tableRoot.ResumeLayout(false);
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.panelStatus.ResumeLayout(false);
            this.panelStatusLeft.ResumeLayout(false);
            this.panelStatusLeft.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).EndInit();
            this.panelFooter.ResumeLayout(false);
            this.gbGdm.ResumeLayout(false);
            this.gbGdm.PerformLayout();
            this.gbRelay.ResumeLayout(false);
            this.gbRelay.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage tabTest;
        private System.Windows.Forms.TabPage tabDevice;
        private System.Windows.Forms.TableLayoutPanel tableRoot;
        private System.Windows.Forms.TableLayoutPanel panelTop;
        private System.Windows.Forms.Label lblBarcodeCaption;
        private System.Windows.Forms.TextBox txtBarcode;
        private System.Windows.Forms.Label lblBarcodeMsg;
        private System.Windows.Forms.TableLayoutPanel panelStatus;
        private System.Windows.Forms.TableLayoutPanel panelStatusLeft;
        private System.Windows.Forms.Label capStep;
        private System.Windows.Forms.Label lblCurrentStep;
        private System.Windows.Forms.Label capRelay;
        private System.Windows.Forms.Label lblRelay;
        private System.Windows.Forms.Label capMeasure;
        private System.Windows.Forms.Label lblMeasure;
        private System.Windows.Forms.Label lblResult;
        private System.Windows.Forms.DataGridView dgvResults;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStep;
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRelay;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMode;
        private System.Windows.Forms.DataGridViewTextBoxColumn colValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLimit;
        private System.Windows.Forms.DataGridViewTextBoxColumn colResult;
        private System.Windows.Forms.Panel panelFooter;
        private System.Windows.Forms.Label lblInfo;
        private System.Windows.Forms.Label lblConnGdm;
        private System.Windows.Forms.Label lblConnRelay;
        private System.Windows.Forms.GroupBox gbGdm;
        private System.Windows.Forms.Label lblGdmPortCap;
        private System.Windows.Forms.ComboBox cbGdmPort;
        private System.Windows.Forms.Button btnGdmRefresh;
        private System.Windows.Forms.Label lblGdmBaudCap;
        private System.Windows.Forms.ComboBox cbGdmBaud;
        private System.Windows.Forms.Button btnGdmConnect;
        private System.Windows.Forms.Button btnGdmDisconnect;
        private System.Windows.Forms.Label lblGdmStatus;
        private System.Windows.Forms.Label lblGdmIdn;
        private System.Windows.Forms.RadioButton rbSerial;
        private System.Windows.Forms.RadioButton rbLan;
        private System.Windows.Forms.Label lblGdmIpCap;
        private System.Windows.Forms.TextBox txtGdmIp;
        private System.Windows.Forms.Label lblGdmTcpPortCap;
        private System.Windows.Forms.TextBox txtGdmPort;
        private System.Windows.Forms.GroupBox gbRelay;
        private System.Windows.Forms.Label lblRelayInfo;
        private System.Windows.Forms.Label lblRelayStatus;
        private System.Windows.Forms.Button btnRelayConnect;
        private System.Windows.Forms.Button btnRelayDisconnect;
    }
}
