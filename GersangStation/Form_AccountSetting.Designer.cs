namespace GersangStation {
    partial class Form_AccountSetting {
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
            this.materialButton2 = new MaterialSkin.Controls.MaterialButton();
            this.materialListBox1 = new MaterialSkin.Controls.MaterialListBox();
            this.materialButton_addAccount = new MaterialSkin.Controls.MaterialButton();
            this.materialButton_close = new MaterialSkin.Controls.MaterialButton();
            this.label3 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // materialButton2
            // 
            this.materialButton2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.materialButton2.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.materialButton2.Depth = 0;
            this.materialButton2.HighEmphasis = true;
            this.materialButton2.Icon = null;
            this.materialButton2.Location = new System.Drawing.Point(257, 142);
            this.materialButton2.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.materialButton2.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialButton2.Name = "materialButton2";
            this.materialButton2.NoAccentTextColor = System.Drawing.Color.Empty;
            this.materialButton2.Size = new System.Drawing.Size(64, 36);
            this.materialButton2.TabIndex = 45;
            this.materialButton2.TabStop = false;
            this.materialButton2.Text = "삭제";
            this.materialButton2.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.materialButton2.UseAccentColor = false;
            this.materialButton2.UseVisualStyleBackColor = true;
            this.materialButton2.Click += new System.EventHandler(this.materialButton2_Click);
            // 
            // materialListBox1
            // 
            this.materialListBox1.BackColor = System.Drawing.Color.White;
            this.materialListBox1.BorderColor = System.Drawing.Color.LightGray;
            this.materialListBox1.Depth = 0;
            this.materialListBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.materialListBox1.Location = new System.Drawing.Point(17, 101);
            this.materialListBox1.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialListBox1.Name = "materialListBox1";
            this.materialListBox1.SelectedIndex = -1;
            this.materialListBox1.SelectedItem = null;
            this.materialListBox1.Size = new System.Drawing.Size(233, 160);
            this.materialListBox1.TabIndex = 44;
            this.materialListBox1.TabStop = false;
            // 
            // materialButton_addAccount
            // 
            this.materialButton_addAccount.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.materialButton_addAccount.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.materialButton_addAccount.Depth = 0;
            this.materialButton_addAccount.HighEmphasis = true;
            this.materialButton_addAccount.Icon = null;
            this.materialButton_addAccount.Location = new System.Drawing.Point(257, 99);
            this.materialButton_addAccount.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.materialButton_addAccount.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialButton_addAccount.Name = "materialButton_addAccount";
            this.materialButton_addAccount.NoAccentTextColor = System.Drawing.Color.Empty;
            this.materialButton_addAccount.Size = new System.Drawing.Size(64, 36);
            this.materialButton_addAccount.TabIndex = 43;
            this.materialButton_addAccount.Text = "추가";
            this.materialButton_addAccount.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.materialButton_addAccount.UseAccentColor = false;
            this.materialButton_addAccount.UseVisualStyleBackColor = true;
            this.materialButton_addAccount.Click += new System.EventHandler(this.materialButton_addAccount_Click);
            // 
            // materialButton_close
            // 
            this.materialButton_close.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.materialButton_close.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.materialButton_close.Depth = 0;
            this.materialButton_close.HighEmphasis = true;
            this.materialButton_close.Icon = null;
            this.materialButton_close.Location = new System.Drawing.Point(140, 279);
            this.materialButton_close.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.materialButton_close.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialButton_close.Name = "materialButton_close";
            this.materialButton_close.NoAccentTextColor = System.Drawing.Color.Empty;
            this.materialButton_close.Size = new System.Drawing.Size(64, 36);
            this.materialButton_close.TabIndex = 47;
            this.materialButton_close.Text = "닫기";
            this.materialButton_close.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.materialButton_close.UseAccentColor = false;
            this.materialButton_close.UseVisualStyleBackColor = true;
            this.materialButton_close.Click += new System.EventHandler(this.materialButton_close_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label3.Font = new System.Drawing.Font("Noto Sans KR", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label3.Location = new System.Drawing.Point(17, 79);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(57, 19);
            this.label3.TabIndex = 100;
            this.label3.Text = "계정목록";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // Form_AccountSetting
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(342, 335);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.materialButton_close);
            this.Controls.Add(this.materialButton2);
            this.Controls.Add(this.materialListBox1);
            this.Controls.Add(this.materialButton_addAccount);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form_AccountSetting";
            this.ShowInTaskbar = false;
            this.Sizable = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "계정 설정";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.Form_AccountSetting_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private MaterialSkin.Controls.MaterialButton materialButton2;
        private MaterialSkin.Controls.MaterialListBox materialListBox1;
        private MaterialSkin.Controls.MaterialButton materialButton_addAccount;
        private MaterialSkin.Controls.MaterialButton materialButton_close;
        private Label label3;
    }
}