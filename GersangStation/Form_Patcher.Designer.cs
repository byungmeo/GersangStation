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
            if (disposing && (components != null)) {
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
            this.listView = new System.Windows.Forms.ListView();
            this.fileName = new System.Windows.Forms.ColumnHeader();
            this.filePath = new System.Windows.Forms.ColumnHeader();
            this.downloaded = new System.Windows.Forms.ColumnHeader();
            this.fileSize = new System.Windows.Forms.ColumnHeader();
            this.status = new System.Windows.Forms.ColumnHeader();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.materialCard1 = new MaterialSkin.Controls.MaterialCard();
            this.label2 = new System.Windows.Forms.Label();
            this.textBox_latestVersion = new System.Windows.Forms.TextBox();
            this.textBox_currentVersion = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.materialCard2 = new MaterialSkin.Controls.MaterialCard();
            this.textBox_folderName_3 = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.textBox_folderName_2 = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textBox_mainPath = new System.Windows.Forms.TextBox();
            this.materialCheckbox_apply = new MaterialSkin.Controls.MaterialCheckbox();
            this.materialCheckbox_delete = new MaterialSkin.Controls.MaterialCheckbox();
            this.materialButton_startPatch = new MaterialSkin.Controls.MaterialButton();
            this.materialButton_close = new MaterialSkin.Controls.MaterialButton();
            this.label6 = new System.Windows.Forms.Label();
            this.label_progress = new System.Windows.Forms.Label();
            this.materialCard1.SuspendLayout();
            this.materialCard2.SuspendLayout();
            this.SuspendLayout();
            // 
            // listView
            // 
            this.listView.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.listView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.fileName,
            this.filePath,
            this.downloaded,
            this.fileSize,
            this.status});
            this.listView.Location = new System.Drawing.Point(6, 258);
            this.listView.Name = "listView";
            this.listView.Size = new System.Drawing.Size(611, 244);
            this.listView.TabIndex = 34;
            this.listView.UseCompatibleStateImageBehavior = false;
            this.listView.View = System.Windows.Forms.View.Details;
            // 
            // fileName
            // 
            this.fileName.Text = "파일명";
            this.fileName.Width = 140;
            // 
            // filePath
            // 
            this.filePath.Text = "경로";
            this.filePath.Width = 140;
            // 
            // downloaded
            // 
            this.downloaded.Text = "다운로드";
            this.downloaded.Width = 80;
            // 
            // fileSize
            // 
            this.fileSize.Text = "파일크기";
            this.fileSize.Width = 80;
            // 
            // status
            // 
            this.status.Text = "상태";
            this.status.Width = 139;
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(306, 206);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(215, 36);
            this.progressBar.TabIndex = 35;
            // 
            // materialCard1
            // 
            this.materialCard1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.materialCard1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.materialCard1.Controls.Add(this.label2);
            this.materialCard1.Controls.Add(this.textBox_latestVersion);
            this.materialCard1.Controls.Add(this.textBox_currentVersion);
            this.materialCard1.Controls.Add(this.label1);
            this.materialCard1.Depth = 0;
            this.materialCard1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(222)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.materialCard1.Location = new System.Drawing.Point(6, 108);
            this.materialCard1.Margin = new System.Windows.Forms.Padding(14);
            this.materialCard1.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialCard1.Name = "materialCard1";
            this.materialCard1.Padding = new System.Windows.Forms.Padding(14);
            this.materialCard1.Size = new System.Drawing.Size(191, 83);
            this.materialCard1.TabIndex = 36;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label2.Font = new System.Drawing.Font("Noto Sans KR", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label2.Location = new System.Drawing.Point(23, 47);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(69, 19);
            this.label2.TabIndex = 37;
            this.label2.Text = "최신 버전 :";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textBox_latestVersion
            // 
            this.textBox_latestVersion.Location = new System.Drawing.Point(98, 47);
            this.textBox_latestVersion.Name = "textBox_latestVersion";
            this.textBox_latestVersion.ReadOnly = true;
            this.textBox_latestVersion.Size = new System.Drawing.Size(62, 23);
            this.textBox_latestVersion.TabIndex = 36;
            this.textBox_latestVersion.Text = "00000";
            this.textBox_latestVersion.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // textBox_currentVersion
            // 
            this.textBox_currentVersion.Location = new System.Drawing.Point(98, 12);
            this.textBox_currentVersion.Name = "textBox_currentVersion";
            this.textBox_currentVersion.ReadOnly = true;
            this.textBox_currentVersion.Size = new System.Drawing.Size(62, 23);
            this.textBox_currentVersion.TabIndex = 34;
            this.textBox_currentVersion.Text = "00000";
            this.textBox_currentVersion.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label1.Font = new System.Drawing.Font("Noto Sans KR", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label1.Location = new System.Drawing.Point(23, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(69, 19);
            this.label1.TabIndex = 33;
            this.label1.Text = "현재 버전 :";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // materialCard2
            // 
            this.materialCard2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.materialCard2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.materialCard2.Controls.Add(this.textBox_folderName_3);
            this.materialCard2.Controls.Add(this.label5);
            this.materialCard2.Controls.Add(this.label4);
            this.materialCard2.Controls.Add(this.textBox_folderName_2);
            this.materialCard2.Controls.Add(this.label3);
            this.materialCard2.Controls.Add(this.textBox_mainPath);
            this.materialCard2.Depth = 0;
            this.materialCard2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(222)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.materialCard2.Location = new System.Drawing.Point(213, 78);
            this.materialCard2.Margin = new System.Windows.Forms.Padding(14);
            this.materialCard2.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialCard2.Name = "materialCard2";
            this.materialCard2.Padding = new System.Windows.Forms.Padding(14);
            this.materialCard2.Size = new System.Drawing.Size(404, 113);
            this.materialCard2.TabIndex = 37;
            // 
            // textBox_folderName_3
            // 
            this.textBox_folderName_3.Location = new System.Drawing.Point(107, 81);
            this.textBox_folderName_3.Name = "textBox_folderName_3";
            this.textBox_folderName_3.ReadOnly = true;
            this.textBox_folderName_3.Size = new System.Drawing.Size(100, 23);
            this.textBox_folderName_3.TabIndex = 42;
            this.textBox_folderName_3.Text = "Gersang3";
            this.textBox_folderName_3.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label5.Font = new System.Drawing.Font("Noto Sans KR", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label5.Location = new System.Drawing.Point(15, 81);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(76, 19);
            this.label5.TabIndex = 41;
            this.label5.Text = "3클 폴더명 :";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label4.Font = new System.Drawing.Font("Noto Sans KR", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label4.Location = new System.Drawing.Point(15, 46);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(76, 19);
            this.label4.TabIndex = 40;
            this.label4.Text = "2클 폴더명 :";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textBox_folderName_2
            // 
            this.textBox_folderName_2.Location = new System.Drawing.Point(107, 46);
            this.textBox_folderName_2.Name = "textBox_folderName_2";
            this.textBox_folderName_2.ReadOnly = true;
            this.textBox_folderName_2.Size = new System.Drawing.Size(100, 23);
            this.textBox_folderName_2.TabIndex = 39;
            this.textBox_folderName_2.Text = "Gersang2";
            this.textBox_folderName_2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label3.Font = new System.Drawing.Font("Noto Sans KR", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label3.Location = new System.Drawing.Point(10, 14);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(81, 19);
            this.label3.TabIndex = 38;
            this.label3.Text = "본클라 경로 :";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textBox_mainPath
            // 
            this.textBox_mainPath.Location = new System.Drawing.Point(107, 14);
            this.textBox_mainPath.Name = "textBox_mainPath";
            this.textBox_mainPath.ReadOnly = true;
            this.textBox_mainPath.Size = new System.Drawing.Size(280, 23);
            this.textBox_mainPath.TabIndex = 37;
            this.textBox_mainPath.Text = "G:\\AKInteractive\\Gersang";
            this.textBox_mainPath.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // materialCheckbox_apply
            // 
            this.materialCheckbox_apply.AutoSize = true;
            this.materialCheckbox_apply.Checked = true;
            this.materialCheckbox_apply.CheckState = System.Windows.Forms.CheckState.Checked;
            this.materialCheckbox_apply.Depth = 0;
            this.materialCheckbox_apply.Location = new System.Drawing.Point(11, 205);
            this.materialCheckbox_apply.Margin = new System.Windows.Forms.Padding(0);
            this.materialCheckbox_apply.MouseLocation = new System.Drawing.Point(-1, -1);
            this.materialCheckbox_apply.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialCheckbox_apply.Name = "materialCheckbox_apply";
            this.materialCheckbox_apply.ReadOnly = false;
            this.materialCheckbox_apply.Ripple = true;
            this.materialCheckbox_apply.Size = new System.Drawing.Size(127, 37);
            this.materialCheckbox_apply.TabIndex = 38;
            this.materialCheckbox_apply.Text = "다클라 패치 적용";
            this.materialCheckbox_apply.UseVisualStyleBackColor = true;
            // 
            // materialCheckbox_delete
            // 
            this.materialCheckbox_delete.AutoSize = true;
            this.materialCheckbox_delete.Checked = true;
            this.materialCheckbox_delete.CheckState = System.Windows.Forms.CheckState.Checked;
            this.materialCheckbox_delete.Depth = 0;
            this.materialCheckbox_delete.Location = new System.Drawing.Point(154, 205);
            this.materialCheckbox_delete.Margin = new System.Windows.Forms.Padding(0);
            this.materialCheckbox_delete.MouseLocation = new System.Drawing.Point(-1, -1);
            this.materialCheckbox_delete.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialCheckbox_delete.Name = "materialCheckbox_delete";
            this.materialCheckbox_delete.ReadOnly = false;
            this.materialCheckbox_delete.Ripple = true;
            this.materialCheckbox_delete.Size = new System.Drawing.Size(131, 37);
            this.materialCheckbox_delete.TabIndex = 39;
            this.materialCheckbox_delete.Text = "패치 후 파일 삭제";
            this.materialCheckbox_delete.UseVisualStyleBackColor = true;
            // 
            // materialButton_startPatch
            // 
            this.materialButton_startPatch.AutoSize = false;
            this.materialButton_startPatch.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.materialButton_startPatch.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.materialButton_startPatch.Depth = 0;
            this.materialButton_startPatch.HighEmphasis = true;
            this.materialButton_startPatch.Icon = null;
            this.materialButton_startPatch.Location = new System.Drawing.Point(528, 206);
            this.materialButton_startPatch.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.materialButton_startPatch.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialButton_startPatch.Name = "materialButton_startPatch";
            this.materialButton_startPatch.NoAccentTextColor = System.Drawing.Color.Empty;
            this.materialButton_startPatch.Size = new System.Drawing.Size(89, 36);
            this.materialButton_startPatch.TabIndex = 40;
            this.materialButton_startPatch.Text = "패치 시작";
            this.materialButton_startPatch.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.materialButton_startPatch.UseAccentColor = true;
            this.materialButton_startPatch.UseVisualStyleBackColor = true;
            this.materialButton_startPatch.Click += new System.EventHandler(this.materialButton_startPatch_Click);
            // 
            // materialButton_close
            // 
            this.materialButton_close.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.materialButton_close.AutoSize = false;
            this.materialButton_close.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.materialButton_close.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.materialButton_close.Depth = 0;
            this.materialButton_close.HighEmphasis = true;
            this.materialButton_close.Icon = null;
            this.materialButton_close.Location = new System.Drawing.Point(274, 513);
            this.materialButton_close.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.materialButton_close.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialButton_close.Name = "materialButton_close";
            this.materialButton_close.NoAccentTextColor = System.Drawing.Color.Empty;
            this.materialButton_close.Size = new System.Drawing.Size(75, 36);
            this.materialButton_close.TabIndex = 41;
            this.materialButton_close.Text = "닫기";
            this.materialButton_close.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.materialButton_close.UseAccentColor = false;
            this.materialButton_close.UseVisualStyleBackColor = true;
            this.materialButton_close.Click += new System.EventHandler(this.materialButton_close_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label6.Font = new System.Drawing.Font("Noto Sans KR", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label6.ForeColor = System.Drawing.Color.Blue;
            this.label6.Location = new System.Drawing.Point(18, 75);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(164, 22);
            this.label6.TabIndex = 42;
            this.label6.Text = "거상 패치가 가능합니다!";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label_progress
            // 
            this.label_progress.AutoSize = true;
            this.label_progress.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.label_progress.Location = new System.Drawing.Point(393, 217);
            this.label_progress.Name = "label_progress";
            this.label_progress.Size = new System.Drawing.Size(34, 15);
            this.label_progress.TabIndex = 43;
            this.label_progress.Text = "0 / 0";
            this.label_progress.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // Form_Patcher
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(623, 562);
            this.Controls.Add(this.label_progress);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.materialButton_close);
            this.Controls.Add(this.materialButton_startPatch);
            this.Controls.Add(this.materialCheckbox_delete);
            this.Controls.Add(this.materialCheckbox_apply);
            this.Controls.Add(this.materialCard2);
            this.Controls.Add(this.materialCard1);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.listView);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form_Patcher";
            this.ShowInTaskbar = false;
            this.Sizable = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "GersangPatcher";
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_Patcher_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form_Patcher_FormClosed);
            this.Load += new System.EventHandler(this.Form_Patcher_Load);
            this.materialCard1.ResumeLayout(false);
            this.materialCard1.PerformLayout();
            this.materialCard2.ResumeLayout(false);
            this.materialCard2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private ListView listView;
        private ColumnHeader fileName;
        private ColumnHeader filePath;
        private ColumnHeader downloaded;
        private ColumnHeader fileSize;
        private ColumnHeader status;
        private ProgressBar progressBar;
        private MaterialSkin.Controls.MaterialCard materialCard1;
        private TextBox textBox_currentVersion;
        private Label label1;
        private TextBox textBox_latestVersion;
        private MaterialSkin.Controls.MaterialCard materialCard2;
        private TextBox textBox_mainPath;
        private Label label2;
        private Label label3;
        private Label label4;
        private TextBox textBox_folderName_2;
        private Label label5;
        private TextBox textBox_folderName_3;
        private MaterialSkin.Controls.MaterialCheckbox materialCheckbox_apply;
        private MaterialSkin.Controls.MaterialCheckbox materialCheckbox_delete;
        private MaterialSkin.Controls.MaterialButton materialButton_startPatch;
        private MaterialSkin.Controls.MaterialButton materialButton_close;
        private Label label6;
        private Label label_progress;
    }
}