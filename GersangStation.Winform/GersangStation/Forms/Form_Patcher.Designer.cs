namespace GersangStation {
    partial class Form_Patcher {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if(disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            components = new System.ComponentModel.Container();
            materialCheckbox_delete = new MaterialSkin.Controls.MaterialCheckbox();
            materialCheckbox_apply = new MaterialSkin.Controls.MaterialCheckbox();
            materialCard1 = new MaterialSkin.Controls.MaterialCard();
            label2 = new Label();
            textBox_latestVersion = new TextBox();
            textBox_currentVersion = new TextBox();
            label1 = new Label();
            toolTip1 = new ToolTip(components);
            materialButton_startPatch = new MaterialSkin.Controls.MaterialButton();
            progressBar = new MaterialSkin.Controls.MaterialProgressBar();
            linkLabel_qa = new LinkLabel();
            materialCard1.SuspendLayout();
            SuspendLayout();
            // 
            // materialCheckbox_delete
            // 
            materialCheckbox_delete.AutoSize = true;
            materialCheckbox_delete.Checked = true;
            materialCheckbox_delete.CheckState = CheckState.Checked;
            materialCheckbox_delete.Depth = 0;
            materialCheckbox_delete.Location = new Point(212, 125);
            materialCheckbox_delete.Margin = new Padding(0);
            materialCheckbox_delete.MouseLocation = new Point(-1, -1);
            materialCheckbox_delete.MouseState = MaterialSkin.MouseState.HOVER;
            materialCheckbox_delete.Name = "materialCheckbox_delete";
            materialCheckbox_delete.ReadOnly = false;
            materialCheckbox_delete.Ripple = true;
            materialCheckbox_delete.Size = new Size(138, 37);
            materialCheckbox_delete.TabIndex = 41;
            materialCheckbox_delete.Text = "패치 후 파일 삭제";
            materialCheckbox_delete.UseVisualStyleBackColor = true;
            // 
            // materialCheckbox_apply
            // 
            materialCheckbox_apply.AutoSize = true;
            materialCheckbox_apply.Checked = true;
            materialCheckbox_apply.CheckState = CheckState.Checked;
            materialCheckbox_apply.Depth = 0;
            materialCheckbox_apply.Location = new Point(212, 83);
            materialCheckbox_apply.Margin = new Padding(0);
            materialCheckbox_apply.MouseLocation = new Point(-1, -1);
            materialCheckbox_apply.MouseState = MaterialSkin.MouseState.HOVER;
            materialCheckbox_apply.Name = "materialCheckbox_apply";
            materialCheckbox_apply.ReadOnly = false;
            materialCheckbox_apply.Ripple = true;
            materialCheckbox_apply.Size = new Size(134, 37);
            materialCheckbox_apply.TabIndex = 40;
            materialCheckbox_apply.Text = "다클라 패치 적용";
            materialCheckbox_apply.UseVisualStyleBackColor = true;
            // 
            // materialCard1
            // 
            materialCard1.BackColor = Color.FromArgb(255, 255, 255);
            materialCard1.BorderStyle = BorderStyle.FixedSingle;
            materialCard1.Controls.Add(label2);
            materialCard1.Controls.Add(textBox_latestVersion);
            materialCard1.Controls.Add(textBox_currentVersion);
            materialCard1.Controls.Add(label1);
            materialCard1.Depth = 0;
            materialCard1.ForeColor = Color.FromArgb(222, 0, 0, 0);
            materialCard1.Location = new Point(18, 81);
            materialCard1.Margin = new Padding(14);
            materialCard1.MouseState = MaterialSkin.MouseState.HOVER;
            materialCard1.Name = "materialCard1";
            materialCard1.Padding = new Padding(14);
            materialCard1.Size = new Size(181, 83);
            materialCard1.TabIndex = 43;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.FlatStyle = FlatStyle.System;
            label2.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            label2.Location = new Point(22, 48);
            label2.Name = "label2";
            label2.Size = new Size(60, 16);
            label2.TabIndex = 37;
            label2.Text = "최신 버전 :";
            label2.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // textBox_latestVersion
            // 
            textBox_latestVersion.Location = new Point(88, 46);
            textBox_latestVersion.Name = "textBox_latestVersion";
            textBox_latestVersion.ReadOnly = true;
            textBox_latestVersion.Size = new Size(62, 23);
            textBox_latestVersion.TabIndex = 36;
            textBox_latestVersion.Text = "00000";
            textBox_latestVersion.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox_currentVersion
            // 
            textBox_currentVersion.Location = new Point(88, 11);
            textBox_currentVersion.Name = "textBox_currentVersion";
            textBox_currentVersion.Size = new Size(62, 23);
            textBox_currentVersion.TabIndex = 34;
            textBox_currentVersion.Text = "00000";
            textBox_currentVersion.TextAlign = HorizontalAlignment.Center;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.FlatStyle = FlatStyle.System;
            label1.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            label1.Location = new Point(22, 13);
            label1.Name = "label1";
            label1.Size = new Size(60, 16);
            label1.TabIndex = 33;
            label1.Text = "현재 버전 :";
            label1.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // materialButton_startPatch
            // 
            materialButton_startPatch.AutoSize = false;
            materialButton_startPatch.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            materialButton_startPatch.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            materialButton_startPatch.Depth = 0;
            materialButton_startPatch.HighEmphasis = true;
            materialButton_startPatch.Icon = null;
            materialButton_startPatch.Location = new Point(16, 209);
            materialButton_startPatch.Margin = new Padding(4, 6, 4, 6);
            materialButton_startPatch.MouseState = MaterialSkin.MouseState.HOVER;
            materialButton_startPatch.Name = "materialButton_startPatch";
            materialButton_startPatch.NoAccentTextColor = Color.Empty;
            materialButton_startPatch.Size = new Size(336, 36);
            materialButton_startPatch.TabIndex = 74;
            materialButton_startPatch.Text = "패치시작";
            materialButton_startPatch.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            materialButton_startPatch.UseAccentColor = false;
            materialButton_startPatch.UseVisualStyleBackColor = true;
            materialButton_startPatch.Click += materialButton_startPatch_Click;
            // 
            // progressBar
            // 
            progressBar.Depth = 0;
            progressBar.Location = new Point(16, 203);
            progressBar.MouseState = MaterialSkin.MouseState.HOVER;
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(336, 5);
            progressBar.TabIndex = 75;
            // 
            // linkLabel_qa
            // 
            linkLabel_qa.Location = new Point(35, 177);
            linkLabel_qa.Name = "linkLabel_qa";
            linkLabel_qa.Size = new Size(298, 23);
            linkLabel_qa.TabIndex = 108;
            linkLabel_qa.TabStop = true;
            linkLabel_qa.Text = "※ 똑똑하게 패치하는 방법 및 오류 발생 시 해결 방법";
            linkLabel_qa.LinkClicked += linkLabel_qa_LinkClicked;
            // 
            // Form_Patcher
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(365, 259);
            Controls.Add(linkLabel_qa);
            Controls.Add(progressBar);
            Controls.Add(materialButton_startPatch);
            Controls.Add(materialCard1);
            Controls.Add(materialCheckbox_delete);
            Controls.Add(materialCheckbox_apply);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Form_Patcher";
            ShowInTaskbar = false;
            Sizable = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "GersangPatcher";
            TopMost = true;
            FormClosing += Form_Patcher_FormClosing;
            Load += Form_Patcher_v2_Load;
            materialCard1.ResumeLayout(false);
            materialCard1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MaterialSkin.Controls.MaterialCheckbox materialCheckbox_delete;
        private MaterialSkin.Controls.MaterialCheckbox materialCheckbox_apply;
        private MaterialSkin.Controls.MaterialCard materialCard1;
        private Label label2;
        private TextBox textBox_latestVersion;
        private TextBox textBox_currentVersion;
        private Label label1;
        private ToolTip toolTip1;
        private MaterialSkin.Controls.MaterialButton materialButton_startPatch;
        private MaterialSkin.Controls.MaterialProgressBar progressBar;
        private LinkLabel linkLabel_qa;
    }
}