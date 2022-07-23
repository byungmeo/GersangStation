namespace GersangStation
{
    partial class Form_Patcher_v2
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form_Patcher_v2));
            this.materialCheckbox_delete = new MaterialSkin.Controls.MaterialCheckbox();
            this.materialCheckbox_apply = new MaterialSkin.Controls.MaterialCheckbox();
            this.label6 = new System.Windows.Forms.Label();
            this.materialCard1 = new MaterialSkin.Controls.MaterialCard();
            this.label2 = new System.Windows.Forms.Label();
            this.textBox_latestVersion = new System.Windows.Forms.TextBox();
            this.textBox_currentVersion = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.materialButton_startPatch = new GersangStation.CustomButton();
            this.materialButton_close = new GersangStation.CustomButton();
            this.label_status = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.label_total = new System.Windows.Forms.Label();
            this.materialCard1.SuspendLayout();
            this.SuspendLayout();
            // 
            // materialCheckbox_delete
            // 
            this.materialCheckbox_delete.AutoSize = true;
            this.materialCheckbox_delete.Checked = true;
            this.materialCheckbox_delete.CheckState = System.Windows.Forms.CheckState.Checked;
            this.materialCheckbox_delete.Depth = 0;
            this.materialCheckbox_delete.Location = new System.Drawing.Point(212, 114);
            this.materialCheckbox_delete.Margin = new System.Windows.Forms.Padding(0);
            this.materialCheckbox_delete.MouseLocation = new System.Drawing.Point(-1, -1);
            this.materialCheckbox_delete.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialCheckbox_delete.Name = "materialCheckbox_delete";
            this.materialCheckbox_delete.ReadOnly = false;
            this.materialCheckbox_delete.Ripple = true;
            this.materialCheckbox_delete.Size = new System.Drawing.Size(131, 37);
            this.materialCheckbox_delete.TabIndex = 41;
            this.materialCheckbox_delete.Text = "패치 후 파일 삭제";
            this.materialCheckbox_delete.UseVisualStyleBackColor = true;
            // 
            // materialCheckbox_apply
            // 
            this.materialCheckbox_apply.AutoSize = true;
            this.materialCheckbox_apply.Checked = true;
            this.materialCheckbox_apply.CheckState = System.Windows.Forms.CheckState.Checked;
            this.materialCheckbox_apply.Depth = 0;
            this.materialCheckbox_apply.Location = new System.Drawing.Point(212, 75);
            this.materialCheckbox_apply.Margin = new System.Windows.Forms.Padding(0);
            this.materialCheckbox_apply.MouseLocation = new System.Drawing.Point(-1, -1);
            this.materialCheckbox_apply.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialCheckbox_apply.Name = "materialCheckbox_apply";
            this.materialCheckbox_apply.ReadOnly = false;
            this.materialCheckbox_apply.Ripple = true;
            this.materialCheckbox_apply.Size = new System.Drawing.Size(127, 37);
            this.materialCheckbox_apply.TabIndex = 40;
            this.materialCheckbox_apply.Text = "다클라 패치 적용";
            this.materialCheckbox_apply.UseVisualStyleBackColor = true;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label6.ForeColor = System.Drawing.Color.SeaGreen;
            this.label6.Location = new System.Drawing.Point(18, 75);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(152, 18);
            this.label6.TabIndex = 45;
            this.label6.Text = "거상 패치가 가능합니다!";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
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
            this.materialCard1.Size = new System.Drawing.Size(181, 83);
            this.materialCard1.TabIndex = 43;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label2.Location = new System.Drawing.Point(22, 48);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(60, 16);
            this.label2.TabIndex = 37;
            this.label2.Text = "최신 버전 :";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textBox_latestVersion
            // 
            this.textBox_latestVersion.Location = new System.Drawing.Point(88, 46);
            this.textBox_latestVersion.Name = "textBox_latestVersion";
            this.textBox_latestVersion.ReadOnly = true;
            this.textBox_latestVersion.Size = new System.Drawing.Size(62, 23);
            this.textBox_latestVersion.TabIndex = 36;
            this.textBox_latestVersion.Text = "00000";
            this.textBox_latestVersion.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // textBox_currentVersion
            // 
            this.textBox_currentVersion.Location = new System.Drawing.Point(88, 11);
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
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label1.Location = new System.Drawing.Point(22, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(60, 16);
            this.label1.TabIndex = 33;
            this.label1.Text = "현재 버전 :";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // materialButton_startPatch
            // 
            this.materialButton_startPatch.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(71)))), ((int)(((byte)(79)))));
            this.materialButton_startPatch.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(71)))), ((int)(((byte)(79)))));
            this.materialButton_startPatch.BorderColor = System.Drawing.Color.PaleVioletRed;
            this.materialButton_startPatch.BorderRadius = 5;
            this.materialButton_startPatch.BorderSize = 0;
            this.materialButton_startPatch.FlatAppearance.BorderSize = 0;
            this.materialButton_startPatch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.materialButton_startPatch.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.materialButton_startPatch.ForeColor = System.Drawing.Color.White;
            this.materialButton_startPatch.Image = ((System.Drawing.Image)(resources.GetObject("materialButton_startPatch.Image")));
            this.materialButton_startPatch.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.materialButton_startPatch.Location = new System.Drawing.Point(226, 155);
            this.materialButton_startPatch.Name = "materialButton_startPatch";
            this.materialButton_startPatch.Padding = new System.Windows.Forms.Padding(5);
            this.materialButton_startPatch.Size = new System.Drawing.Size(102, 36);
            this.materialButton_startPatch.TabIndex = 69;
            this.materialButton_startPatch.Text = "패치시작";
            this.materialButton_startPatch.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.materialButton_startPatch.TextColor = System.Drawing.Color.White;
            this.materialButton_startPatch.UseVisualStyleBackColor = false;
            this.materialButton_startPatch.Click += new System.EventHandler(this.materialButton_startPatch_Click);
            // 
            // materialButton_close
            // 
            this.materialButton_close.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(71)))), ((int)(((byte)(79)))));
            this.materialButton_close.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(71)))), ((int)(((byte)(79)))));
            this.materialButton_close.BorderColor = System.Drawing.Color.PaleVioletRed;
            this.materialButton_close.BorderRadius = 5;
            this.materialButton_close.BorderSize = 0;
            this.materialButton_close.FlatAppearance.BorderSize = 0;
            this.materialButton_close.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.materialButton_close.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.materialButton_close.ForeColor = System.Drawing.Color.White;
            this.materialButton_close.Image = ((System.Drawing.Image)(resources.GetObject("materialButton_close.Image")));
            this.materialButton_close.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.materialButton_close.Location = new System.Drawing.Point(142, 260);
            this.materialButton_close.Name = "materialButton_close";
            this.materialButton_close.Padding = new System.Windows.Forms.Padding(5);
            this.materialButton_close.Size = new System.Drawing.Size(79, 36);
            this.materialButton_close.TabIndex = 70;
            this.materialButton_close.Text = "닫기";
            this.materialButton_close.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.materialButton_close.TextColor = System.Drawing.Color.White;
            this.materialButton_close.UseVisualStyleBackColor = false;
            this.materialButton_close.Click += new System.EventHandler(this.materialButton_close_Click);
            // 
            // label_status
            // 
            this.label_status.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label_status.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label_status.ForeColor = System.Drawing.Color.Black;
            this.label_status.Location = new System.Drawing.Point(6, 205);
            this.label_status.Name = "label_status";
            this.label_status.Size = new System.Drawing.Size(353, 18);
            this.label_status.TabIndex = 72;
            this.label_status.Text = "패치 시작 버튼을 누르면 패치가 시작됩니다.";
            this.label_status.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label_total
            // 
            this.label_total.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label_total.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label_total.ForeColor = System.Drawing.Color.Black;
            this.label_total.Location = new System.Drawing.Point(6, 234);
            this.label_total.Name = "label_total";
            this.label_total.Size = new System.Drawing.Size(353, 18);
            this.label_total.TabIndex = 73;
            this.label_total.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // Form_Patcher_v2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(365, 302);
            this.Controls.Add(this.label_total);
            this.Controls.Add(this.label_status);
            this.Controls.Add(this.materialButton_close);
            this.Controls.Add(this.materialButton_startPatch);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.materialCard1);
            this.Controls.Add(this.materialCheckbox_delete);
            this.Controls.Add(this.materialCheckbox_apply);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form_Patcher_v2";
            this.ShowInTaskbar = false;
            this.Sizable = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "GersangPatcher v2";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.Form_Patcher_v2_Load);
            this.materialCard1.ResumeLayout(false);
            this.materialCard1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private MaterialSkin.Controls.MaterialCheckbox materialCheckbox_delete;
        private MaterialSkin.Controls.MaterialCheckbox materialCheckbox_apply;
        private Label label6;
        private MaterialSkin.Controls.MaterialCard materialCard1;
        private Label label2;
        private TextBox textBox_latestVersion;
        private TextBox textBox_currentVersion;
        private Label label1;
        private CustomButton materialButton_startPatch;
        private CustomButton materialButton_close;
        private Label label_status;
        private ToolTip toolTip1;
        private Label label_total;
    }
}